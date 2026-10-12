use serde::Serialize;

#[derive(Debug, Clone, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct Overview {
    pub app_version: String,
    pub build_sha: String,
    pub is_elevated: bool,
}

pub fn build_overview(is_elevated: bool) -> Overview {
    Overview {
        app_version: env!("CARGO_PKG_VERSION").to_string(),
        build_sha: env!("BUILD_SHA").to_string(),
        is_elevated,
    }
}

pub fn is_elevated() -> bool {
    // SAFETY: IsUserAnAdmin takes no arguments and has no preconditions.
    unsafe { windows_sys::Win32::UI::Shell::IsUserAnAdmin() != 0 }
}

#[tauri::command]
pub fn get_overview() -> Overview {
    build_overview(is_elevated())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn overview_reports_package_version_and_sha() {
        let o = build_overview(true);
        assert_eq!(o.app_version, env!("CARGO_PKG_VERSION"));
        assert!(!o.build_sha.is_empty());
        assert!(o.is_elevated);
    }

    #[test]
    fn overview_serializes_camel_case() {
        let json = serde_json::to_value(build_overview(false)).unwrap();
        assert!(json.get("appVersion").is_some());
        assert!(json.get("buildSha").is_some());
        assert_eq!(json["isElevated"], false);
    }
}
