from typing import Optional
import customtkinter as ctk
from ui.theme import THEME, FONTS

class BentoCard(ctk.CTkFrame):
    """Modern Glassmorphism Bento Metric Tile."""

    def __init__(
        self,
        master,
        title: str,
        value: str,
        subtitle: str,
        icon: str = "⚡",
        accent_color: str = "#6366F1",
        mode: str = "dark",
        on_click = None,
        **kwargs
    ):
        theme = THEME[mode]
        super().__init__(
            master,
            fg_color=theme["bg_card"],
            corner_radius=12,
            border_width=1,
            border_color=theme["card_border"],
            **kwargs
        )
        self.mode = mode
        self.accent_color = accent_color

        # Inner container with padding
        self.container = ctk.CTkFrame(self, fg_color="transparent")
        self.container.pack(fill="both", expand=True, padx=14, pady=12)

        # Header Row: Icon badge + Title
        self.header_row = ctk.CTkFrame(self.container, fg_color="transparent")
        self.header_row.pack(fill="x")

        # Icon pill
        self.icon_badge = ctk.CTkLabel(
            self.header_row,
            text=icon,
            font=("Segoe UI", 12),
            fg_color=self.accent_color,
            text_color="#FFFFFF",
            corner_radius=8,
            width=28,
            height=28
        )
        self.icon_badge.pack(side="left")

        # Title
        self.lbl_title = ctk.CTkLabel(
            self.header_row,
            text=title,
            font=FONTS["small_bold"],
            text_color=theme["text_secondary"]
        )
        self.lbl_title.pack(side="left", padx=8)

        # Value Row (Large Metric)
        self.lbl_value = ctk.CTkLabel(
            self.container,
            text=value,
            font=FONTS["display"],
            text_color=theme["text_primary"]
        )
        self.lbl_value.pack(anchor="w", pady=(6, 2))

        # Subtitle / Indicator line
        self.lbl_subtitle = ctk.CTkLabel(
            self.container,
            text=subtitle,
            font=FONTS["small"],
            text_color=theme["text_muted"]
        )
        self.lbl_subtitle.pack(anchor="w")

        if on_click:
            self.bind("<Button-1>", lambda e: on_click())
            self.container.bind("<Button-1>", lambda e: on_click())
            self.lbl_value.bind("<Button-1>", lambda e: on_click())

    def update_data(self, value: str, subtitle: Optional[str] = None):
        self.lbl_value.configure(text=value)
        if subtitle is not None:
            self.lbl_subtitle.configure(text=subtitle)

    def set_theme_mode(self, mode: str):
        self.mode = mode
        theme = THEME[mode]
        self.configure(fg_color=theme["bg_card"], border_color=theme["card_border"])
        self.lbl_title.configure(text_color=theme["text_secondary"])
        self.lbl_value.configure(text_color=theme["text_primary"])
        self.lbl_subtitle.configure(text_color=theme["text_muted"])
