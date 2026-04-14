/// Logger module — configures `tracing` with file and optional console output.
///
/// Log files are written next to the executable using `tracing-appender`.

use tracing_appender::non_blocking::WorkerGuard;
use tracing_subscriber::{fmt, prelude::*, EnvFilter};

/// Initialise the global tracing subscriber.
///
/// Logs are written to a rolling file in `log_dir` and optionally echoed to stderr.
/// Returns the [`WorkerGuard`] — the caller must hold it for the process lifetime
/// so that buffered log writes are flushed on shutdown.
///
/// Creates `log_dir` if it does not already exist.
pub fn init(log_dir: &std::path::Path, level: &str) -> WorkerGuard {
    std::fs::create_dir_all(log_dir).expect("failed to create log directory");

    let file_appender = tracing_appender::rolling::daily(log_dir, "magic-zones.log");
    let (non_blocking, guard) = tracing_appender::non_blocking(file_appender);

    let filter = EnvFilter::try_new(level).unwrap_or_else(|_| EnvFilter::new("info"));

    tracing_subscriber::registry()
        .with(
            fmt::layer()
                .with_writer(non_blocking)
                .with_ansi(false)
                .with_target(true),
        )
        .with(filter)
        .try_init()
        .ok();

    guard
}

/// Initialise a stderr-only subscriber for integration tests.
///
/// Uses `try_init` so it is safe to call from multiple tests — only the first
/// call actually installs a subscriber; subsequent calls are no-ops.
#[allow(dead_code)]
pub fn init_for_test() {
    let filter = EnvFilter::try_new("debug").unwrap_or_else(|_| EnvFilter::new("info"));

    tracing_subscriber::registry()
        .with(
            fmt::layer()
                .with_writer(std::io::stderr)
                .with_ansi(false)
                .with_target(true),
        )
        .with(filter)
        .try_init()
        .ok();
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn init_returns_guard() {
        // Use a unique subdirectory to avoid conflicts with other tests.
        let dir = std::env::current_dir()
            .unwrap()
            .join("test_logs_guard");
        let _guard: WorkerGuard = init(&dir, "info");
        // If this compiles and runs, the guard is correctly returned.
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn init_creates_log_directory() {
        let dir = std::env::current_dir()
            .unwrap()
            .join("test_logs_create");
        // Ensure it doesn't exist before calling init.
        let _ = std::fs::remove_dir_all(&dir);
        assert!(!dir.exists());

        let _guard = init(&dir, "info");
        assert!(dir.exists(), "init() should create the log directory");

        let _ = std::fs::remove_dir_all(&dir);
    }
}
