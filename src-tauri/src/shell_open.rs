//! Opening a web page from an elevated app without starting the browser as administrator
//! (ADR 030): ask the desktop's own Explorer to open it, so it runs as the signed-in person.
//! If that isn't possible, nothing opens; there is deliberately no fallback to opening it here.

/// Opens `url` (already checked by `safe_url`) through Explorer, on its own COM thread.
pub fn open_unelevated(url: &str) -> Result<(), String> {
    let url = url.to_string();
    std::thread::spawn(move || shell_execute_via_desktop(&url))
        .join()
        .map_err(|_| "Couldn't open the link.".to_string())?
}

fn shell_execute_via_desktop(url: &str) -> Result<(), String> {
    use windows::core::{Interface, BSTR};
    use windows::Win32::System::Com::{
        CoCreateInstance, CoInitializeEx, CoUninitialize, IDispatch, IServiceProvider, CLSCTX_ALL,
        COINIT_APARTMENTTHREADED,
    };
    use windows::Win32::System::Variant::VARIANT;
    use windows::Win32::UI::Shell::{
        IShellBrowser, IShellDispatch2, IShellFolderViewDual, IShellWindows, SID_STopLevelBrowser,
        ShellWindows, SVGIO_BACKGROUND, SWC_DESKTOP, SWFO_NEEDDISPATCH,
    };

    // SAFETY: COM is initialised for this thread only and released below.
    let init = unsafe { CoInitializeEx(None, COINIT_APARTMENTTHREADED) };
    if init.is_err() {
        return Err("Couldn't open the link.".into());
    }
    // SAFETY: plain COM calls on interfaces obtained above; every result is checked.
    let result = unsafe {
        (|| -> windows::core::Result<()> {
            let windows: IShellWindows = CoCreateInstance(&ShellWindows, None, CLSCTX_ALL)?;
            let empty = VARIANT::default();
            let mut hwnd = 0i32;
            let desktop: IDispatch =
                windows.FindWindowSW(&empty, &empty, SWC_DESKTOP, &mut hwnd, SWFO_NEEDDISPATCH)?;
            let sp: IServiceProvider = desktop.cast()?;
            let browser: IShellBrowser = sp.QueryService(&SID_STopLevelBrowser)?;
            let view = browser.QueryActiveShellView()?;
            let background: IDispatch = view.GetItemObject(SVGIO_BACKGROUND)?;
            let folder_view: IShellFolderViewDual = background.cast()?;
            let app = folder_view.Application()?;
            let shell: IShellDispatch2 = app.cast()?;
            shell.ShellExecute(
                &BSTR::from(url),
                &VARIANT::default(),
                &VARIANT::default(),
                &VARIANT::default(),
                &VARIANT::default(),
            )
        })()
    };
    // SAFETY: matches the successful CoInitializeEx above.
    unsafe { CoUninitialize() };
    result.map_err(|_| "Couldn't open the link.".into())
}
