//! Windows Task Scheduler through its COM interface (no `schtasks`, no temp files). Each call
//! runs on its own short-lived MTA thread so COM state never leaks into the async runtime.

use super::autostart::{TaskInfo, TASK_FOLDER};
use windows::core::{Interface, BSTR};
use windows::Win32::System::Com::{
    CoCreateInstance, CoInitializeEx, CoUninitialize, CLSCTX_INPROC_SERVER, COINIT_MULTITHREADED,
};
use windows::Win32::System::TaskScheduler::{
    IExecAction, ITaskFolder, ITaskService, TaskScheduler, TASK_CREATE_OR_UPDATE,
    TASK_LOGON_INTERACTIVE_TOKEN,
};
use windows::Win32::System::Variant::{VARIANT, VARIANT_0, VARIANT_0_0, VARIANT_0_0_0, VT_I4};

/// A 32-bit integer VARIANT (collection index).
fn variant_i4(v: i32) -> VARIANT {
    VARIANT {
        Anonymous: VARIANT_0 {
            Anonymous: std::mem::ManuallyDrop::new(VARIANT_0_0 {
                vt: VT_I4,
                wReserved1: 0,
                wReserved2: 0,
                wReserved3: 0,
                Anonymous: VARIANT_0_0_0 { lVal: v },
            }),
        },
    }
}

fn on_com_thread<T: Send + 'static>(
    f: impl FnOnce() -> windows::core::Result<T> + Send + 'static,
) -> Result<T, String> {
    std::thread::spawn(move || {
        // SAFETY: balanced with CoUninitialize on this same thread.
        unsafe { CoInitializeEx(None, COINIT_MULTITHREADED) }
            .ok()
            .map_err(|e| e.message())?;
        let r = f().map_err(|e| e.message());
        // SAFETY: matches the CoInitializeEx above.
        unsafe { CoUninitialize() };
        r
    })
    .join()
    .map_err(|_| "Task Scheduler call crashed".to_string())?
}

fn service() -> windows::core::Result<ITaskService> {
    // SAFETY: plain in-process COM activation on an initialised MTA thread.
    let s: ITaskService = unsafe { CoCreateInstance(&TaskScheduler, None, CLSCTX_INPROC_SERVER)? };
    let none = VARIANT::default();
    // SAFETY: connect to the local Task Scheduler as the current user.
    unsafe { s.Connect(&none, &none, &none, &none)? };
    Ok(s)
}

fn our_folder(s: &ITaskService, create: bool) -> windows::core::Result<Option<ITaskFolder>> {
    // SAFETY: COM calls on valid interfaces.
    unsafe {
        if let Ok(f) = s.GetFolder(&BSTR::from(TASK_FOLDER)) {
            return Ok(Some(f));
        }
        if !create {
            return Ok(None);
        }
        let root = s.GetFolder(&BSTR::from("\\"))?;
        Ok(Some(root.CreateFolder(
            &BSTR::from(TASK_FOLDER.trim_start_matches('\\')),
            &VARIANT::default(),
        )?))
    }
}

/// What our task for `name` points at, if it exists.
pub fn get(name: String) -> Result<Option<TaskInfo>, String> {
    on_com_thread(move || {
        let s = service()?;
        let Some(folder) = our_folder(&s, false)? else {
            return Ok(None);
        };
        // SAFETY: COM calls on valid interfaces.
        unsafe {
            let Ok(task) = folder.GetTask(&BSTR::from(name)) else {
                return Ok(None);
            };
            let actions = task.Definition()?.Actions()?;
            let exec: IExecAction = actions.get_Item(1)?.cast()?;
            let mut path = BSTR::new();
            let mut args = BSTR::new();
            exec.Path(&mut path)?;
            exec.Arguments(&mut args)?;
            Ok(Some(TaskInfo {
                exe: path.to_string(),
                args: args.to_string(),
            }))
        }
    })
}

/// Creates or replaces our task from the in-memory definition.
pub fn register(name: String, xml: String) -> Result<(), String> {
    on_com_thread(move || {
        let s = service()?;
        let folder = our_folder(&s, true)?.expect("created");
        let none = VARIANT::default();
        // SAFETY: COM call on a valid folder; the XML is our own definition.
        unsafe {
            folder.RegisterTask(
                &BSTR::from(name),
                &BSTR::from(xml),
                TASK_CREATE_OR_UPDATE.0,
                &none,
                &none,
                TASK_LOGON_INTERACTIVE_TOKEN,
                &none,
            )?
        };
        Ok(())
    })
}

pub fn delete(name: String) -> Result<(), String> {
    on_com_thread(move || {
        let s = service()?;
        if let Some(folder) = our_folder(&s, false)? {
            // SAFETY: COM call on a valid folder; a missing task is fine.
            let _ = unsafe { folder.DeleteTask(&BSTR::from(name), 0) };
        }
        Ok(())
    })
}

pub struct ComTaskStore;

impl super::autostart::TaskStore for ComTaskStore {
    fn get(&self, name: &str) -> Result<Option<TaskInfo>, String> {
        get(name.to_string())
    }
    fn register(&self, name: &str, xml: &str) -> Result<(), String> {
        register(name.to_string(), xml.to_string())
    }
    fn delete(&self, name: &str) -> Result<(), String> {
        delete(name.to_string())
    }
}

/// Uninstall: removes every "Quantum Updater …" task in our folder, then the folder if empty.
pub fn delete_all() -> Result<(), String> {
    on_com_thread(move || {
        let s = service()?;
        let Some(folder) = our_folder(&s, false)? else {
            return Ok(());
        };
        // SAFETY: COM calls on valid interfaces; indexes are 1-based.
        unsafe {
            let tasks = folder.GetTasks(1)?; // include hidden
            let mut names = Vec::new();
            for i in 1..=tasks.Count()? {
                let t = tasks.get_Item(&variant_i4(i))?;
                let n = t.Name()?.to_string();
                if n.starts_with("Quantum Updater ") {
                    names.push(n);
                }
            }
            for n in names {
                let _ = folder.DeleteTask(&BSTR::from(n), 0);
            }
            if folder.GetTasks(1)?.Count()? == 0 {
                let root = s.GetFolder(&BSTR::from("\\"))?;
                let _ = root.DeleteFolder(&BSTR::from(TASK_FOLDER.trim_start_matches('\\')), 0);
            }
        }
        Ok(())
    })
}
