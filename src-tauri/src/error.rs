use serde::Serialize;
use ts_rs::TS;

/// Error returned by every command: a stable machine `code` plus a human `message`.
#[derive(Debug, Clone, Serialize, PartialEq, Eq, TS)]
#[ts(export)]
pub struct AppError {
    pub code: String,
    pub message: String,
}

pub type AppResult<T> = Result<T, AppError>;

impl AppError {
    pub fn new(code: &str, message: impl Into<String>) -> Self {
        Self {
            code: code.to_string(),
            message: message.into(),
        }
    }
}

impl std::fmt::Display for AppError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}: {}", self.code, self.message)
    }
}

impl std::error::Error for AppError {}

impl From<std::io::Error> for AppError {
    fn from(e: std::io::Error) -> Self {
        let code = match e.kind() {
            std::io::ErrorKind::NotFound => "source_missing",
            std::io::ErrorKind::TimedOut => "timeout",
            _ => "process_failed",
        };
        AppError::new(code, e.to_string())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn serializes_code_and_message() {
        let e = AppError::new("invalid_input", "bad id");
        assert_eq!(
            serde_json::to_string(&e).unwrap(),
            r#"{"code":"invalid_input","message":"bad id"}"#
        );
    }

    #[test]
    fn io_not_found_maps_to_source_missing() {
        let e: AppError = std::io::Error::from(std::io::ErrorKind::NotFound).into();
        assert_eq!(e.code, "source_missing");
    }
}
