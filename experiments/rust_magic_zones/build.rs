fn main() {
    // Re-run if the DPI awareness manifest changes.
    println!("cargo:rerun-if-changed=app.manifest");

    // On Windows the manifest can be embedded with the `embed-resource` or
    // `winres` crate.  The runtime `SetProcessDpiAwarenessContext` call in
    // main.rs is the primary mechanism; the manifest is belt-and-suspenders.
    #[cfg(windows)]
    {
        // If embed-resource is available as a build dependency:
        //   embed_resource::compile("app.manifest", embed_resource::NONE);
        //
        // Otherwise the manifest can be applied post-build with:
        //   mt.exe -manifest app.manifest -outputresource:magic-zones.exe;#1
    }
}
