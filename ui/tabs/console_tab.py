import customtkinter as ctk
import tkinter as tk
from ui.theme import THEME, FONTS

class ConsoleTab(ctk.CTkFrame):
    """Real-time console output view for observing hidden background execution."""

    def __init__(self, master, mode: str = "dark", **kwargs):
        super().__init__(master, fg_color="transparent", **kwargs)
        self.mode = mode

        theme = THEME[self.mode]

        # Container Frame
        self.container = ctk.CTkFrame(self, fg_color="transparent")
        self.container.pack(fill="both", expand=True, padx=20, pady=(15, 10))

        # Top Bar
        self.top_bar = ctk.CTkFrame(
            self.container,
            fg_color=theme["bg_card"],
            corner_radius=12,
            border_width=1,
            border_color=theme["card_border"]
        )
        self.top_bar.pack(fill="x", pady=(0, 12))

        inner = ctk.CTkFrame(self.top_bar, fg_color="transparent")
        inner.pack(fill="x", padx=16, pady=12)

        self.title_label = ctk.CTkLabel(
            inner,
            text="Activity & Process Console",
            font=FONTS["subtitle"],
            text_color=theme["text_primary"]
        )
        self.title_label.pack(side="left")

        self.btn_copy = ctk.CTkButton(
            inner,
            text="📋 Copy All",
            width=85,
            height=30,
            fg_color=theme["bg_card_hover"],
            hover_color=theme["primary"],
            text_color=theme["text_primary"],
            font=FONTS["small_bold"],
            corner_radius=6,
            command=self.copy_log
        )
        self.btn_copy.pack(side="right", padx=(6, 0))

        self.btn_clear = ctk.CTkButton(
            inner,
            text="🗑️ Clear Log",
            width=85,
            height=30,
            fg_color=theme["bg_card_hover"],
            hover_color=theme["danger"],
            text_color=theme["text_primary"],
            font=FONTS["small_bold"],
            corner_radius=6,
            command=self.clear_log
        )
        self.btn_clear.pack(side="right")

        # Terminal container with glass border
        self.terminal_frame = ctk.CTkFrame(
            self.container,
            fg_color=theme["terminal_bg"],
            corner_radius=12,
            border_width=1,
            border_color=theme["card_border"]
        )
        self.terminal_frame.pack(fill="both", expand=True)

        # Console text area
        self.textbox = ctk.CTkTextbox(
            self.terminal_frame,
            fg_color="transparent",
            text_color=theme["terminal_text"],
            font=FONTS["mono"],
            corner_radius=12,
            wrap="word"
        )
        self.textbox.pack(fill="both", expand=True, padx=8, pady=8)

        # Initial log
        self.append_log("[WinPackageHub]: Engine initialized in silent background mode. Ready.\n")

    def append_log(self, text: str):
        """Thread-safe log appending."""
        def _append():
            self.textbox.configure(state="normal")
            self.textbox.insert("end", text)
            self.textbox.see("end")
            self.textbox.configure(state="disabled")

        self.after(0, _append)

    def clear_log(self):
        self.textbox.configure(state="normal")
        self.textbox.delete("1.0", "end")
        self.textbox.insert("end", "[WinPackageHub]: Log cleared.\n")
        self.textbox.configure(state="disabled")

    def copy_log(self):
        content = self.textbox.get("1.0", "end")
        self.clipboard_clear()
        self.clipboard_append(content)

    def set_theme_mode(self, mode: str):
        self.mode = mode
        theme = THEME[mode]
        self.top_bar.configure(fg_color=theme["bg_card"], border_color=theme["card_border"])
        self.title_label.configure(text_color=theme["text_primary"])
        self.btn_clear.configure(fg_color=theme["bg_card_hover"], text_color=theme["text_primary"])
        self.btn_copy.configure(fg_color=theme["bg_card_hover"], text_color=theme["text_primary"])
        self.terminal_frame.configure(fg_color=theme["terminal_bg"], border_color=theme["card_border"])
        self.textbox.configure(text_color=theme["terminal_text"])
