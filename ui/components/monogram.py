import customtkinter as ctk
from ui.theme import get_monogram_colors, FONTS

class AppMonogram(ctk.CTkFrame):
    """Dynamic rounded icon badge with initials for application visual anchoring."""

    def __init__(self, master, app_name: str, size: int = 38, **kwargs):
        bg_col, text_col = get_monogram_colors(app_name)
        super().__init__(
            master,
            width=size,
            height=size,
            corner_radius=10,
            fg_color=bg_col,
            **kwargs
        )
        self.pack_propagate(False)

        # Compute clean initials (1 to 2 uppercase chars)
        initials = self._extract_initials(app_name)

        self.lbl = ctk.CTkLabel(
            self,
            text=initials,
            font=("Segoe UI Variable Display", int(size * 0.38), "bold"),
            text_color=text_col
        )
        self.lbl.place(relx=0.5, rely=0.5, anchor="center")

    def _extract_initials(self, name: str) -> str:
        words = [w for w in name.replace("-", " ").replace(".", " ").split() if w]
        if not words:
            return "AP"
        if len(words) == 1:
            return words[0][:2].upper()
        return (words[0][0] + words[1][0]).upper()
