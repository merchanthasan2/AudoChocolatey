//! Test double for `Runner`: answers by subcommand (first argv element) and records every call.

use crate::error::{AppError, AppResult};
use crate::sources::process::{CommandSpec, ProcessOutput, Runner};
use async_trait::async_trait;
use std::collections::HashMap;
use std::sync::Mutex;

#[derive(Default)]
pub struct FakeRunner {
    responses: HashMap<String, AppResult<ProcessOutput>>,
    export_json: Option<String>,
    calls: Mutex<Vec<Vec<String>>>,
}

impl FakeRunner {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn on(mut self, sub: &str, code: i32, stdout: &str) -> Self {
        let out = ProcessOutput {
            code: Some(code),
            stdout: stdout.to_string(),
            stderr: String::new(),
        };
        self.responses.insert(sub.to_string(), Ok(out));
        self
    }

    pub fn fail(mut self, sub: &str, err: AppError) -> Self {
        self.responses.insert(sub.to_string(), Err(err));
        self
    }

    /// Content written to the `-o <path>` file when `export` is invoked.
    pub fn export(mut self, json: &str) -> Self {
        self.export_json = Some(json.to_string());
        self.on("export", 0, "")
    }

    pub fn calls(&self) -> Vec<Vec<String>> {
        self.calls.lock().unwrap().clone()
    }
}

#[async_trait]
impl Runner for FakeRunner {
    async fn run(&self, spec: &CommandSpec) -> AppResult<ProcessOutput> {
        self.calls.lock().unwrap().push(spec.args.clone());
        let sub = spec.args.first().cloned().unwrap_or_default();
        if sub == "export" {
            if let (Some(json), Some(i)) =
                (&self.export_json, spec.args.iter().position(|a| a == "-o"))
            {
                std::fs::write(&spec.args[i + 1], json).unwrap();
            }
        }
        self.responses.get(&sub).cloned().unwrap_or_else(|| {
            Err(AppError::new(
                "process_failed",
                format!("no fake for {sub}"),
            ))
        })
    }
}
