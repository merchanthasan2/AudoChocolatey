import customtkinter as ctk
import tkinter.messagebox as mb
from typing import List, Optional
from core.models import PackageInfo
from ui.theme import THEME, FONTS
from ui.components import AppMonogram

class InstalledTab(ctk.CTkFrame):
    """Tab 3: Glassmorphic Installed Applications Inspector."""

    def __init__(self, master, manager, on_log_callback, mode: str = "dark", **kwargs):
        super().__init__(master, fg_color="transparent", **kwargs)
        self.manager = manager
        self.on_log = on_log_callback
        self.mode = mode
        self.installed_packages: List[PackageInfo] = []

        theme = THEME[self.mode]

        # Main Scroll Container
        self.main_scroll = ctk.CTkScrollableFrame(
            self,
            fg_color="transparent",
            corner_radius=0
        )
        self.main_scroll.pack(fill="both", expand=True, padx=20, pady=(15, 10))

        # Top Glass Control Card
        self.top_card = ctk.CTkFrame(
            self.main_scroll,
            fg_color=theme["bg_card"],
            corner_radius=14,
            border_width=1,
            border_color=theme["card_border"]
        )
        self.top_card.pack(fill="x", pady=(0, 16))

        inner = ctk.CTkFrame(self.top_card, fg_color="transparent")
        inner.pack(fill="x", padx=18, pady=16)

        # Header Title Row
        header_row = ctk.CTkFrame(inner, fg_color="transparent")
        header_row.pack(fill="x", pady=(0, 10))

        title_left = ctk.CTkFrame(header_row, fg_color="transparent")
        title_left.pack(side="left")

        self.lbl_title = ctk.CTkLabel(
            title_left,
            text="Installed Applications",
            font=FONTS["title"],
            text_color=theme["text_primary"]
        )
        self.lbl_title.pack(side="left")

        self.badge_count = ctk.CTkLabel(
            title_left,
            text="0 Audited",
            fg_color=theme["bg_secondary"],
            text_color=theme["text_muted"],
            font=FONTS["small_bold"],
            corner_radius=12,
            padx=10,
            pady=3
        )
        self.badge_count.pack(side="left", padx=12)

        self.lbl_sub = ctk.CTkLabel(
            inner,
            text="Inspect applications registered on your system, verify current versions, or safely remove unneeded packages.",
            font=FONTS["body"],
            text_color=theme["text_muted"],
            wraplength=700,
            justify="left"
        )
        self.lbl_sub.pack(anchor="w", pady=(0, 12))

        # Action Bar Row
        action_row = ctk.CTkFrame(inner, fg_color="transparent")
        action_row.pack(fill="x")

        self.btn_refresh = ctk.CTkButton(
            action_row,
            text="🔄 Audit System Packages",
            font=FONTS["body_bold"],
            fg_color=theme["primary"],
            hover_color=theme["primary_hover"],
            height=38,
            corner_radius=8,
            command=self.load_installed
        )
        self.btn_refresh.pack(side="left", padx=(0, 14))

        self.entry_filter = ctk.CTkEntry(
            action_row,
            placeholder_text="Filter installed software by name or package ID...",
            font=FONTS["body"],
            height=38,
            fg_color=theme["bg_input"],
            border_color=theme["card_border"],
            corner_radius=8
        )
        self.entry_filter.pack(side="right", fill="x", expand=True)
        self.entry_filter.bind("<KeyRelease>", self.on_filter)

        # Progress bar
        self.progress_bar = ctk.CTkProgressBar(
            self.top_card,
            height=3,
            fg_color="#1E293B" if self.mode == "dark" else "#E2E8F0",
            progress_color=theme["primary"],
            corner_radius=0
        )
        self.progress_bar.pack(fill="x", side="bottom")
        self.progress_bar.set(0)

        # Package List Container
        self.list_container = ctk.CTkFrame(self.main_scroll, fg_color="transparent")
        self.list_container.pack(fill="both", expand=True)

        self.show_placeholder("Click 'Audit System Packages' to scan installed software on this machine.")

    def show_placeholder(self, text: str):
        for widget in self.list_container.winfo_children():
            widget.destroy()

        theme = THEME[self.mode]
        frame = ctk.CTkFrame(
            self.list_container,
            fg_color=theme["bg_card"],
            corner_radius=14,
            border_width=1,
            border_color=theme["card_border"]
        )
        frame.pack(fill="both", expand=True, pady=16)

        lbl = ctk.CTkLabel(
            frame,
            text=text,
            font=FONTS["subtitle"],
            text_color=theme["text_muted"],
            wraplength=650
        )
        lbl.pack(padx=24, pady=48)

    def load_installed(self):
        if self.manager.is_busy:
            return

        theme = THEME[self.mode]
        self.btn_refresh.configure(state="disabled", text="Auditing System...")
        self.progress_bar.configure(mode="indeterminate")
        self.progress_bar.start()
        self.show_placeholder("Reading installed packages from system registry and package manager database...")

        def on_success(packages: List[PackageInfo]):
            def _ui():
                self.progress_bar.stop()
                self.progress_bar.configure(mode="determinate")
                self.progress_bar.set(1.0)
                self.btn_refresh.configure(state="normal", text="🔄 Audit System Packages")
                self.installed_packages = packages
                self.render_packages(packages)
            self.after(0, _ui)

        def on_error(err: Exception):
            def _ui():
                self.progress_bar.stop()
                self.progress_bar.configure(mode="determinate")
                self.progress_bar.set(0)
                self.btn_refresh.configure(state="normal", text="🔄 Audit System Packages")
                self.show_placeholder(f"Failed to audit installed packages: {str(err)}")
            self.after(0, _ui)

        self.manager.list_installed_async(
            filter_query=None,
            on_log=self.on_log,
            on_success=on_success,
            on_error=on_error
        )

    def render_packages(self, packages: List[PackageInfo]):
        for widget in self.list_container.winfo_children():
            widget.destroy()

        theme = THEME[self.mode]
        count = len(packages)
        self.badge_count.configure(
            text=f"{count} Installed",
            fg_color=theme["accent_glow"] if count > 0 else theme["bg_secondary"],
            text_color=theme["accent"] if count > 0 else theme["text_muted"]
        )

        if not packages:
            self.show_placeholder("No installed packages match your filter.")
            return

        for pkg in packages:
            self._create_package_card(pkg)

    def _create_package_card(self, pkg: PackageInfo):
        theme = THEME[self.mode]

        card = ctk.CTkFrame(
            self.list_container,
            fg_color=theme["bg_card"],
            corner_radius=12,
            border_width=1,
            border_color=theme["card_border"]
        )
        card.pack(fill="x", pady=4)

        # Content Layout: Monogram | Info | Actions
        row = ctk.CTkFrame(card, fg_color="transparent")
        row.pack(fill="x", padx=14, pady=10)

        # Monogram
        mono = AppMonogram(row, app_name=pkg.name, size=40)
        mono.pack(side="left", padx=(0, 12))

        # Center Details
        details = ctk.CTkFrame(row, fg_color="transparent")
        details.pack(side="left", fill="both", expand=True)

        name_row = ctk.CTkFrame(details, fg_color="transparent")
        name_row.pack(fill="x", anchor="w")

        lbl_name = ctk.CTkLabel(
            name_row,
            text=pkg.name,
            font=FONTS["body_bold"],
            text_color=theme["text_primary"],
            anchor="w"
        )
        lbl_name.pack(side="left")

        # Sub row: ID + Version Pill
        meta_row = ctk.CTkFrame(details, fg_color="transparent")
        meta_row.pack(fill="x", anchor="w", pady=(2, 0))

        lbl_id = ctk.CTkLabel(
            meta_row,
            text=pkg.id,
            font=FONTS["small"],
            text_color=theme["text_muted"],
            anchor="w"
        )
        lbl_id.pack(side="left")

        if pkg.installed_version:
            lbl_ver = ctk.CTkLabel(
                meta_row,
                text=f"v{pkg.installed_version}",
                font=FONTS["small_bold"],
                fg_color=theme["bg_secondary"],
                text_color=theme["accent"],
                corner_radius=6,
                padx=8,
                pady=2
            )
            lbl_ver.pack(side="left", padx=10)

        # Uninstall Button
        btn_uninstall = ctk.CTkButton(
            row,
            text="Uninstall",
            font=FONTS["small_bold"],
            fg_color=theme["bg_card_hover"],
            hover_color=theme["danger"],
            text_color=theme["text_secondary"],
            width=90,
            height=32,
            corner_radius=8
        )
        btn_uninstall.configure(command=lambda p=pkg, b=btn_uninstall: self.uninstall_package(p, b))
        btn_uninstall.pack(side="right", padx=(8, 0))

    def uninstall_package(self, pkg: PackageInfo, btn: ctk.CTkButton):
        if self.manager.is_busy:
            return

        confirm = mb.askyesno(
            "Confirm Uninstall",
            f"Are you sure you want to uninstall '{pkg.name}' ({pkg.id})?"
        )
        if not confirm:
            return

        theme = THEME[self.mode]
        btn.configure(state="disabled", text="Removing...", fg_color=theme["warning"], text_color="#FFFFFF")

        def on_success(res):
            def _ui():
                if res.success:
                    btn.configure(text="✓ Removed", fg_color=theme["success"])
                    self.load_installed()
                else:
                    btn.configure(text="Retry", fg_color=theme["danger"], state="normal")
            self.after(0, _ui)

        def on_error(err):
            def _ui():
                btn.configure(text="Error", fg_color=theme["danger"], state="normal")
            self.after(0, _ui)

        self.manager.uninstall_package_async(
            package_id=pkg.id,
            on_log=self.on_log,
            on_success=on_success,
            on_error=on_error
        )

    def on_filter(self, event=None):
        query = self.entry_filter.get().strip().lower()
        if not query:
            self.render_packages(self.installed_packages)
            return

        filtered = [
            p for p in self.installed_packages
            if query in p.name.lower() or query in p.id.lower()
        ]
        self.render_packages(filtered)

    def set_theme_mode(self, mode: str):
        self.mode = mode
        theme = THEME[mode]
        self.top_card.configure(fg_color=theme["bg_card"], border_color=theme["card_border"])
        self.lbl_title.configure(text_color=theme["text_primary"])
        self.lbl_sub.configure(text_color=theme["text_muted"])
        self.entry_filter.configure(fg_color=theme["bg_input"], border_color=theme["card_border"])
        self.btn_refresh.configure(fg_color=theme["primary"], hover_color=theme["primary_hover"])
        self.render_packages(self.installed_packages)
