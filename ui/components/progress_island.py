from typing import Optional
import customtkinter as ctk
from ui.theme import THEME, FONTS

class ProgressIsland(ctk.CTkFrame):
    """Dynamic Progress Island: Floating Glass Capsule for active operations."""

    def __init__(self, master, mode: str = "dark", **kwargs):
        theme = THEME[mode]
        super().__init__(
            master,
            fg_color=theme["island_bg"],
            corner_radius=14,
            border_width=1,
            border_color=theme["island_border"],
            **kwargs
        )
        self.mode = mode

        # Content container
        self.inner = ctk.CTkFrame(self, fg_color="transparent")
        self.inner.pack(fill="both", expand=True, padx=16, pady=12)

        # Header Row
        self.top_row = ctk.CTkFrame(self.inner, fg_color="transparent")
        self.top_row.pack(fill="x")

        # Status pulse indicator
        self.lbl_pulse = ctk.CTkLabel(
            self.top_row,
            text="●",
            font=("Segoe UI", 12, "bold"),
            text_color=theme["cyan"]
        )
        self.lbl_pulse.pack(side="left")

        # Active Task Title
        self.lbl_task_title = ctk.CTkLabel(
            self.top_row,
            text="System Status: Ready",
            font=FONTS["body_bold"],
            text_color=theme["text_primary"]
        )
        self.lbl_task_title.pack(side="left", padx=6)

        # Live Percentage Badge
        self.lbl_pct = ctk.CTkLabel(
            self.top_row,
            text="",
            font=FONTS["small_bold"],
            text_color=theme["cyan"]
        )
        self.lbl_pct.pack(side="right")

        # Live Progress Bar
        self.progress_bar = ctk.CTkProgressBar(
            self.inner,
            height=6,
            fg_color="#1E293B" if mode == "dark" else "#E2E8F0",
            progress_color=theme["primary"],
            corner_radius=4
        )
        self.progress_bar.pack(fill="x", pady=(8, 6))
        self.progress_bar.set(0)

        # Status Ticker line
        self.lbl_ticker = ctk.CTkLabel(
            self.inner,
            text="All operations idle. Select packages below to begin.",
            font=FONTS["mono"],
            text_color=theme["text_muted"],
            anchor="w"
        )
        self.lbl_ticker.pack(fill="x")

    def update_status(
        self,
        title: str,
        ticker_msg: str,
        pct: Optional[float] = None,
        is_active: bool = True
    ):
        """Update live island display."""
        theme = THEME[self.mode]

        self.lbl_task_title.configure(text=title)
        self.lbl_pulse.configure(
            text_color=theme["cyan"] if is_active else theme["text_muted"]
        )

        # Truncate ticker message if needed for clean layout
        truncated = ticker_msg if len(ticker_msg) < 110 else ticker_msg[:107] + "..."
        self.lbl_ticker.configure(
            text=truncated,
            text_color=theme["text_secondary"] if is_active else theme["text_muted"]
        )

        if pct is not None:
            self.progress_bar.configure(mode="determinate")
            self.progress_bar.set(max(0.0, min(1.0, pct)))
            self.lbl_pct.configure(text=f"{int(pct * 100)}%")
        else:
            if is_active:
                self.progress_bar.configure(mode="indeterminate")
                self.progress_bar.start()
                self.lbl_pct.configure(text="Processing...")
            else:
                self.progress_bar.stop()
                self.progress_bar.configure(mode="determinate")
                self.progress_bar.set(0)
                self.lbl_pct.configure(text="")

    def set_theme_mode(self, mode: str):
        self.mode = mode
        theme = THEME[mode]
        self.configure(fg_color=theme["island_bg"], border_color=theme["island_border"])
        self.lbl_task_title.configure(text_color=theme["text_primary"])
        self.progress_bar.configure(
            fg_color="#1E293B" if mode == "dark" else "#E2E8F0",
            progress_color=theme["primary"]
        )
        self.lbl_ticker.configure(text_color=theme["text_muted"])
