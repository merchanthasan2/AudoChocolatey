import os
import sys
import customtkinter as ctk
from core.manager import PackageManager
from core.admin import is_admin
from ui.tabs import UpdatesTab, ExploreTab, InstalledTab, ConsoleTab, SettingsTab
from ui.theme import THEME, FONTS

class MainWindow(ctk.CTk):
    """Main Application Window for WinPackageHub with Human Glassmorphism Design."""

    def __init__(self):
        super().__init__()

        self.manager = PackageManager()
        self.current_mode = self.manager.settings.get("theme", "dark")
        if self.current_mode not in ("dark", "light"):
            self.current_mode = "dark"

        ctk.set_appearance_mode(self.current_mode)
        ctk.set_default_color_theme("blue")

        self.title("WinPackageHub • Package & Update Manager")
        self.geometry("1180x760")
        self.minsize(980, 640)

        # Clean shutdown and abort protocol
        self.protocol("WM_DELETE_WINDOW", self.on_close)

        # Apply root background
        theme = THEME[self.current_mode]
        self.configure(fg_color=theme["bg_main"])

        # Main Layout: Top Glass Bar + Content Area
        self.grid_rowconfigure(1, weight=1)
        self.grid_columnconfigure(0, weight=1)

        self._build_top_bar()
        self._build_content_area()

        # Select initial tab
        self.select_tab("updates")

    def _build_top_bar(self):
        theme = THEME[self.current_mode]

        # Top Glass Navigation Bar
        self.top_bar = ctk.CTkFrame(
            self,
            height=68,
            corner_radius=0,
            fg_color=theme["bg_surface"],
            border_width=1,
            border_color=theme["card_border"]
        )
        self.top_bar.grid(row=0, column=0, sticky="ew")
        self.top_bar.grid_propagate(False)

        # Left: Brand Logo & Tagline
        self.brand_frame = ctk.CTkFrame(self.top_bar, fg_color="transparent")
        self.brand_frame.pack(side="left", padx=(20, 24), pady=12)

        self.lbl_logo = ctk.CTkLabel(
            self.brand_frame,
            text="⚡ WinPackageHub",
            font=FONTS["title"],
            text_color=theme["primary"]
        )
        self.lbl_logo.pack(anchor="w")

        # Center: Sleek Horizontal Pill Tabs
        self.tabs_frame = ctk.CTkFrame(self.top_bar, fg_color="transparent")
        self.tabs_frame.pack(side="left", fill="y", pady=12)

        self.nav_buttons = {}
        nav_items = [
            ("updates", "🔄  Updates"),
            ("explore", "🔍  Explore Store"),
            ("installed", "📦  Installed Apps"),
            ("console", "💻  Activity Log"),
            ("settings", "⚙️  Settings")
        ]

        for tab_id, label in nav_items:
            btn = ctk.CTkButton(
                self.tabs_frame,
                text=label,
                font=FONTS["body_bold"],
                height=38,
                corner_radius=10,
                fg_color="transparent",
                hover_color=theme["bg_card_hover"],
                text_color=theme["text_secondary"],
                command=lambda tid=tab_id: self.select_tab(tid)
            )
            btn.pack(side="left", padx=4)
            self.nav_buttons[tab_id] = btn

        # Right: Status Badges & Theme Toggle
        self.right_frame = ctk.CTkFrame(self.top_bar, fg_color="transparent")
        self.right_frame.pack(side="right", padx=(10, 20), pady=12)

        # Admin Badge
        admin_active = is_admin()
        self.badge_admin = ctk.CTkLabel(
            self.right_frame,
            text="🛡️ Elevated" if admin_active else "⚠️ User Mode",
            font=FONTS["small_bold"],
            fg_color="#064E3B" if admin_active else "#451A03",
            text_color="#6EE7B7" if admin_active else "#FCA5A5",
            corner_radius=8,
            padx=10,
            pady=5
        )
        self.badge_admin.pack(side="left", padx=6)

        # Active Engine Badge
        engine_name = self.manager.current_engine.name.split()[0]
        self.badge_engine = ctk.CTkLabel(
            self.right_frame,
            text=f"Engine: {engine_name}",
            font=FONTS["small_bold"],
            fg_color=theme["bg_card"],
            text_color=theme["text_secondary"],
            corner_radius=8,
            padx=10,
            pady=5
        )
        self.badge_engine.pack(side="left", padx=6)

        # Theme Switcher (Moon / Sun toggle)
        self.btn_theme = ctk.CTkButton(
            self.right_frame,
            text="🌙 Dark" if self.current_mode == "dark" else "☀️ Light",
            font=FONTS["small_bold"],
            width=75,
            height=30,
            corner_radius=8,
            fg_color=theme["bg_card"],
            hover_color=theme["bg_card_hover"],
            text_color=theme["text_primary"],
            command=self.toggle_theme
        )
        self.btn_theme.pack(side="left", padx=(6, 0))

    def _build_content_area(self):
        self.content_container = ctk.CTkFrame(self, fg_color="transparent")
        self.content_container.grid(row=1, column=0, sticky="nsew")

        # Instantiate Tabs with initial theme mode
        self.tab_console = ConsoleTab(self.content_container, mode=self.current_mode)
        self.tab_updates = UpdatesTab(
            self.content_container,
            manager=self.manager,
            on_log_callback=self.tab_console.append_log,
            mode=self.current_mode
        )
        self.tab_explore = ExploreTab(
            self.content_container,
            manager=self.manager,
            on_log_callback=self.tab_console.append_log,
            mode=self.current_mode
        )
        self.tab_installed = InstalledTab(
            self.content_container,
            manager=self.manager,
            on_log_callback=self.tab_console.append_log,
            mode=self.current_mode
        )
        self.tab_settings = SettingsTab(
            self.content_container,
            manager=self.manager,
            on_engine_changed_callback=self.on_engine_switched,
            mode=self.current_mode
        )

        self.tabs = {
            "updates": self.tab_updates,
            "explore": self.tab_explore,
            "installed": self.tab_installed,
            "console": self.tab_console,
            "settings": self.tab_settings
        }

    def select_tab(self, tab_id: str):
        theme = THEME[self.current_mode]

        # Hide all tabs and unselect buttons
        for tid, tab_widget in self.tabs.items():
            tab_widget.pack_forget()
            btn = self.nav_buttons.get(tid)
            if btn:
                btn.configure(
                    fg_color="transparent",
                    text_color=theme["text_secondary"]
                )

        # Show target tab and highlight pill
        if tab_id in self.tabs:
            self.tabs[tab_id].pack(fill="both", expand=True)
            active_btn = self.nav_buttons.get(tab_id)
            if active_btn:
                active_btn.configure(
                    fg_color=theme["primary"],
                    text_color="#FFFFFF"
                )

    def toggle_theme(self):
        """Toggle between Dark and Light Glass themes in real time."""
        new_mode = "light" if self.current_mode == "dark" else "dark"
        self.current_mode = new_mode
        self.manager.settings["theme"] = new_mode
        self.manager.save_settings()

        ctk.set_appearance_mode(new_mode)
        theme = THEME[new_mode]

        self.configure(fg_color=theme["bg_main"])
        self.top_bar.configure(fg_color=theme["bg_surface"], border_color=theme["card_border"])
        self.lbl_logo.configure(text_color=theme["primary"])
        self.badge_engine.configure(fg_color=theme["bg_card"], text_color=theme["text_secondary"])
        self.btn_theme.configure(
            text="🌙 Dark" if new_mode == "dark" else "☀️ Light",
            fg_color=theme["bg_card"],
            hover_color=theme["bg_card_hover"],
            text_color=theme["text_primary"]
        )

        # Update tabs
        for tab in self.tabs.values():
            if hasattr(tab, "set_theme_mode"):
                tab.set_theme_mode(new_mode)

        # Refresh active button state
        for tid, btn in self.nav_buttons.items():
            if self.tabs[tid].winfo_ismapped():
                btn.configure(fg_color=theme["primary"], text_color="#FFFFFF")
            else:
                btn.configure(fg_color="transparent", text_color=theme["text_secondary"], hover_color=theme["bg_card_hover"])

    def on_engine_switched(self, new_engine_id: str):
        engine_name = self.manager.current_engine.name.split()[0]
        self.badge_engine.configure(text=f"Engine: {engine_name}")
        self.tab_console.append_log(f"\n[WinPackageHub]: Switched active engine to {self.manager.current_engine.name}\n")
        # Trigger scan refresh in updates tab
        if hasattr(self.tab_updates, "start_scan"):
            self.tab_updates.start_scan()

    def on_close(self):
        """Immediately abort any running tasks, subprocesses, and exit cleanly."""
        try:
            self.manager.abort_all()
        except Exception:
            pass
        try:
            self.destroy()
        except Exception:
            pass
        os._exit(0)
