import customtkinter as ctk
import platform
import sys
from core.admin import is_admin
from ui.theme import THEME, FONTS

class SettingsTab(ctk.CTkFrame):
    """Tab 5: Glassmorphic Application Settings, Engine Switching, and Diagnostics."""

    def __init__(self, master, manager, on_engine_changed_callback, mode: str = "dark", **kwargs):
        super().__init__(master, fg_color="transparent", **kwargs)
        self.manager = manager
        self.on_engine_changed = on_engine_changed_callback
        self.mode = mode

        # Scrollable container for settings
        self.scroll = ctk.CTkScrollableFrame(self, fg_color="transparent")
        self.scroll.pack(fill="both", expand=True, padx=20, pady=(15, 15))

        # Build sections
        self._build_engine_card()
        self._build_preferences_card()
        self._build_diagnostics_card()

    def _build_engine_card(self):
        theme = THEME[self.mode]

        self.engine_card = ctk.CTkFrame(
            self.scroll,
            fg_color=theme["bg_card"],
            corner_radius=14,
            border_width=1,
            border_color=theme["card_border"]
        )
        self.engine_card.pack(fill="x", pady=(0, 16))

        inner = ctk.CTkFrame(self.engine_card, fg_color="transparent")
        inner.pack(fill="x", padx=18, pady=16)

        self.lbl_eng_title = ctk.CTkLabel(
            inner,
            text="Package Manager Engine",
            font=FONTS["subtitle"],
            text_color=theme["text_primary"]
        )
        self.lbl_eng_title.pack(anchor="w", pady=(0, 4))

        self.lbl_eng_sub = ctk.CTkLabel(
            inner,
            text="Choose which package backend WinPackageHub utilizes for updating and discovering applications.",
            font=FONTS["body"],
            text_color=theme["text_muted"]
        )
        self.lbl_eng_sub.pack(anchor="w", pady=(0, 14))

        self.engine_var = ctk.StringVar(value=self.manager.active_engine_id)
        self.engine_rows = []

        engines = self.manager.get_available_engines()
        for eng in engines:
            row = ctk.CTkFrame(
                inner,
                fg_color=theme["bg_secondary"],
                corner_radius=10,
                border_width=1,
                border_color=theme["card_border"]
            )
            row.pack(fill="x", pady=4)
            self.engine_rows.append(row)

            status_text = f"Installed (v{eng['version']})" if eng['available'] else "Not Installed"
            badge_bg = theme["accent_glow"] if eng['available'] else theme["danger_glow"]
            badge_fg = theme["accent"] if eng['available'] else theme["danger"]

            rb = ctk.CTkRadioButton(
                row,
                text=eng["name"],
                variable=self.engine_var,
                value=eng["id"],
                font=FONTS["body_bold"],
                command=self._on_engine_select,
                state="normal" if eng["available"] else "disabled"
            )
            rb.pack(side="left", padx=14, pady=12)

            badge = ctk.CTkLabel(
                row,
                text=status_text,
                font=FONTS["small_bold"],
                fg_color=badge_bg,
                text_color=badge_fg,
                corner_radius=6,
                padx=10,
                pady=3
            )
            badge.pack(side="right", padx=14, pady=12)

    def _on_engine_select(self):
        new_engine = self.engine_var.get()
        self.manager.set_active_engine(new_engine)
        if self.on_engine_changed:
            self.on_engine_changed(new_engine)

    def _build_preferences_card(self):
        theme = THEME[self.mode]

        self.pref_card = ctk.CTkFrame(
            self.scroll,
            fg_color=theme["bg_card"],
            corner_radius=14,
            border_width=1,
            border_color=theme["card_border"]
        )
        self.pref_card.pack(fill="x", pady=(0, 16))

        inner = ctk.CTkFrame(self.pref_card, fg_color="transparent")
        inner.pack(fill="x", padx=18, pady=16)

        self.lbl_pref_title = ctk.CTkLabel(
            inner,
            text="Automation & Execution Preferences",
            font=FONTS["subtitle"],
            text_color=theme["text_primary"]
        )
        self.lbl_pref_title.pack(anchor="w", pady=(0, 4))

        self.lbl_pref_sub = ctk.CTkLabel(
            inner,
            text="Configure how package installers interact with your Windows environment.",
            font=FONTS["body"],
            text_color=theme["text_muted"]
        )
        self.lbl_pref_sub.pack(anchor="w", pady=(0, 14))

        # Silent switch
        self.switch_silent = ctk.CTkSwitch(
            inner,
            text="Silent Background Execution (Hides native installer wizard dialogs & prompts)",
            font=FONTS["body"],
            command=self._save_preferences
        )
        if self.manager.settings.get("silent_install", True):
            self.switch_silent.select()
        self.switch_silent.pack(anchor="w", pady=6)

        # Agreements switch
        self.switch_agree = ctk.CTkSwitch(
            inner,
            text="Automatically accept repository package licenses and source agreements",
            font=FONTS["body"],
            command=self._save_preferences
        )
        if self.manager.settings.get("accept_agreements", True):
            self.switch_agree.select()
        self.switch_agree.pack(anchor="w", pady=6)

    def _save_preferences(self):
        self.manager.settings["silent_install"] = bool(self.switch_silent.get())
        self.manager.settings["accept_agreements"] = bool(self.switch_agree.get())
        self.manager.save_settings()

    def _build_diagnostics_card(self):
        theme = THEME[self.mode]

        self.diag_card = ctk.CTkFrame(
            self.scroll,
            fg_color=theme["bg_card"],
            corner_radius=14,
            border_width=1,
            border_color=theme["card_border"]
        )
        self.diag_card.pack(fill="x", pady=(0, 16))

        inner = ctk.CTkFrame(self.diag_card, fg_color="transparent")
        inner.pack(fill="x", padx=18, pady=16)

        self.lbl_diag_title = ctk.CTkLabel(
            inner,
            text="System Diagnostics & Environment",
            font=FONTS["subtitle"],
            text_color=theme["text_primary"]
        )
        self.lbl_diag_title.pack(anchor="w", pady=(0, 4))

        self.lbl_diag_sub = ctk.CTkLabel(
            inner,
            text="Runtime environment details and active privilege context.",
            font=FONTS["body"],
            text_color=theme["text_muted"]
        )
        self.lbl_diag_sub.pack(anchor="w", pady=(0, 14))

        self.diag_container = ctk.CTkFrame(inner, fg_color="transparent")
        self.diag_container.pack(fill="x")

        self._render_diag_rows()

    def _render_diag_rows(self):
        for widget in self.diag_container.winfo_children():
            widget.destroy()

        theme = THEME[self.mode]
        diag_info = [
            ("Privilege Context", "🛡️ Administrator (Elevated)" if is_admin() else "⚠️ Standard User (Not Elevated)"),
            ("Operating System", f"{platform.system()} {platform.release()} (Build {platform.version().split('.')[-1] if '.' in platform.version() else platform.version()})"),
            ("CPU Architecture", platform.machine()),
            ("Python Runtime", f"{sys.version.split()[0]} ({sys.executable})"),
            ("Winget Executable", str(self.manager.engines["winget"]._executable or "Not Located")),
            ("Winget Version", str(self.manager.engines["winget"].get_version() or "Not Available")),
            ("Chocolatey Executable", str(self.manager.engines["choco"]._executable or "Not Located")),
            ("Chocolatey Version", str(self.manager.engines["choco"].get_version() or "Not Available"))
        ]

        for label, val in diag_info:
            row = ctk.CTkFrame(
                self.diag_container,
                fg_color=theme["bg_secondary"],
                corner_radius=8,
                border_width=1,
                border_color=theme["card_border"]
            )
            row.pack(fill="x", pady=3)

            lbl_k = ctk.CTkLabel(
                row,
                text=label,
                font=FONTS["small_bold"],
                text_color=theme["text_secondary"]
            )
            lbl_k.pack(side="left", padx=12, pady=8)

            lbl_v = ctk.CTkLabel(
                row,
                text=val,
                font=FONTS["small"],
                text_color=theme["text_muted"]
            )
            lbl_v.pack(side="right", padx=12, pady=8)

    def set_theme_mode(self, mode: str):
        self.mode = mode
        theme = THEME[mode]

        # Engine card
        self.engine_card.configure(fg_color=theme["bg_card"], border_color=theme["card_border"])
        self.lbl_eng_title.configure(text_color=theme["text_primary"])
        self.lbl_eng_sub.configure(text_color=theme["text_muted"])
        for row in self.engine_rows:
            row.configure(fg_color=theme["bg_secondary"], border_color=theme["card_border"])

        # Preferences card
        self.pref_card.configure(fg_color=theme["bg_card"], border_color=theme["card_border"])
        self.lbl_pref_title.configure(text_color=theme["text_primary"])
        self.lbl_pref_sub.configure(text_color=theme["text_muted"])

        # Diagnostics card
        self.diag_card.configure(fg_color=theme["bg_card"], border_color=theme["card_border"])
        self.lbl_diag_title.configure(text_color=theme["text_primary"])
        self.lbl_diag_sub.configure(text_color=theme["text_muted"])
        self._render_diag_rows()
