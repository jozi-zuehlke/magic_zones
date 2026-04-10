/// Config file watcher with debounce support.
///
/// Watches a single config file for changes and sends notifications
/// through a crossbeam channel, with manual debouncing to coalesce
/// rapid filesystem events.

use crossbeam_channel::Sender;
use notify::{EventKind, RecommendedWatcher, RecursiveMode, Watcher};
use parking_lot::Mutex;
use std::path::{Path, PathBuf};
use std::sync::Arc;
use std::time::{Duration, Instant};

pub struct ConfigFileWatcher {
    _watcher: RecommendedWatcher,
}

impl ConfigFileWatcher {
    /// Start watching `path` for changes.
    ///
    /// Sends `()` on `sender` whenever the file is created, modified, or
    /// removed, but no more often than every `debounce_ms` milliseconds.
    pub fn watch(path: &Path, debounce_ms: u64, sender: Sender<()>) -> Result<Self, String> {
        let canonical_path = path
            .canonicalize()
            .or_else(|_| {
                // File may not exist yet — canonicalize the parent and append the filename.
                let parent = path.parent().ok_or("no parent directory")?;
                let file_name = path.file_name().ok_or("no file name")?;
                Ok::<PathBuf, &str>(parent.canonicalize().map_err(|_| "cannot resolve parent")?.join(file_name))
            })
            .map_err(|e| format!("failed to resolve config path: {e}"))?;

        let watch_dir = canonical_path
            .parent()
            .ok_or_else(|| "config file has no parent directory".to_string())?
            .to_path_buf();

        let target_path = canonical_path.clone();
        let debounce_dur = Duration::from_millis(debounce_ms);
        let last_sent: Arc<Mutex<Instant>> = Arc::new(Mutex::new(
            Instant::now() - Duration::from_secs(60), // allow first event immediately
        ));

        let mut watcher = notify::recommended_watcher(move |res: Result<notify::Event, notify::Error>| {
            let event = match res {
                Ok(ev) => ev,
                Err(_) => return,
            };

            let dominated = matches!(
                event.kind,
                EventKind::Create(_) | EventKind::Modify(_) | EventKind::Remove(_)
            );
            if !dominated {
                return;
            }

            let matches_target = event.paths.iter().any(|p| {
                p.canonicalize().map(|c| c == target_path).unwrap_or_else(|_| p == &target_path)
            });
            if !matches_target {
                return;
            }

            let mut last = last_sent.lock();
            if last.elapsed() >= debounce_dur {
                *last = Instant::now();
                let _ = sender.send(());
            }
        })
        .map_err(|e| format!("failed to create watcher: {e}"))?;

        watcher
            .watch(&watch_dir, RecursiveMode::NonRecursive)
            .map_err(|e| format!("failed to watch directory: {e}"))?;

        Ok(Self { _watcher: watcher })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crossbeam_channel::bounded;
    use std::fs;
    use std::thread;
    use std::time::Duration;

    /// Helper: create a temp directory that is cleaned up on drop.
    struct TempDir(PathBuf);
    impl TempDir {
        fn new(name: &str) -> Self {
            let dir = std::env::temp_dir().join(format!("mz_watcher_test_{name}_{}", std::process::id()));
            let _ = fs::remove_dir_all(&dir);
            fs::create_dir_all(&dir).expect("create temp dir");
            Self(dir)
        }
        fn path(&self) -> &Path {
            &self.0
        }
    }
    impl Drop for TempDir {
        fn drop(&mut self) {
            let _ = fs::remove_dir_all(&self.0);
        }
    }

    #[test]
    fn watcher_sends_on_file_modify() {
        let dir = TempDir::new("modify");
        let config_path = dir.path().join("zones.json");
        fs::write(&config_path, r#"{"zones":[]}"#).unwrap();

        let (tx, rx) = bounded::<()>(16);
        let _watcher = ConfigFileWatcher::watch(&config_path, 50, tx).unwrap();

        // Give the OS watcher time to register.
        thread::sleep(Duration::from_millis(200));

        fs::write(&config_path, r#"{"zones":[1]}"#).unwrap();

        let received = rx.recv_timeout(Duration::from_secs(2));
        assert!(received.is_ok(), "expected notification after file modify");
    }

    #[test]
    fn watcher_does_not_send_for_other_files() {
        let dir = TempDir::new("other");
        let config_path = dir.path().join("zones.json");
        fs::write(&config_path, r#"{"zones":[]}"#).unwrap();

        let (tx, rx) = bounded::<()>(16);
        let _watcher = ConfigFileWatcher::watch(&config_path, 50, tx).unwrap();

        thread::sleep(Duration::from_millis(200));

        // Modify a *different* file in the same directory.
        let other_file = dir.path().join("other.txt");
        fs::write(&other_file, "hello").unwrap();

        let received = rx.recv_timeout(Duration::from_millis(500));
        assert!(received.is_err(), "should NOT receive notification for unrelated file");
    }

    #[test]
    fn watcher_debounces_rapid_changes() {
        let dir = TempDir::new("debounce");
        let config_path = dir.path().join("zones.json");
        fs::write(&config_path, r#"{"zones":[]}"#).unwrap();

        let (tx, rx) = bounded::<()>(64);
        let _watcher = ConfigFileWatcher::watch(&config_path, 300, tx).unwrap();

        thread::sleep(Duration::from_millis(200));

        // Fire 5 writes 10ms apart — within the 300ms debounce window.
        for i in 0..5 {
            fs::write(&config_path, format!(r#"{{"v":{i}}}"#)).unwrap();
            thread::sleep(Duration::from_millis(10));
        }

        // Wait long enough for all events to be delivered.
        thread::sleep(Duration::from_millis(600));

        let mut count = 0;
        while rx.try_recv().is_ok() {
            count += 1;
        }
        assert!(
            count >= 1 && count <= 2,
            "expected 1-2 debounced notifications but got {count}"
        );
    }
}
