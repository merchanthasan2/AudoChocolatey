import abc
from typing import List, Callable, Optional
from core.models import PackageInfo, SearchResult, OperationStatus

class BaseEngine(abc.ABC):
    """Abstract Base Class for package managers (Winget, Chocolatey, etc.)"""
    
    @property
    @abc.abstractmethod
    def name(self) -> str:
        """Display name of the engine."""
        pass

    @property
    @abc.abstractmethod
    def identifier(self) -> str:
        """Machine identifier (e.g., 'winget', 'choco')."""
        pass

    @abc.abstractmethod
    def is_available(self) -> bool:
        """Check if the package manager executable is installed and available."""
        pass

    @abc.abstractmethod
    def get_version(self) -> Optional[str]:
        """Return the installed version of the package manager."""
        pass

    @abc.abstractmethod
    def check_updates(self, on_log: Optional[Callable[[str], None]] = None) -> List[PackageInfo]:
        """Scan system for available updates."""
        pass

    @abc.abstractmethod
    def list_installed(self, filter_query: Optional[str] = None, on_log: Optional[Callable[[str], None]] = None) -> List[PackageInfo]:
        """List currently installed packages."""
        pass

    @abc.abstractmethod
    def search_packages(self, query: str, on_log: Optional[Callable[[str], None]] = None) -> List[SearchResult]:
        """Search repositories for packages to install."""
        pass

    @abc.abstractmethod
    def upgrade_package(
        self, 
        package_id: str, 
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True
    ) -> OperationStatus:
        """Upgrade a specific package by ID with progress updates."""
        pass

    @abc.abstractmethod
    def upgrade_all(
        self,
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True
    ) -> OperationStatus:
        """Upgrade all outdated packages."""
        pass

    @abc.abstractmethod
    def install_package(
        self, 
        package_id: str, 
        on_log: Optional[Callable[[str], None]] = None,
        on_progress: Optional[Callable[[Optional[float], str], None]] = None,
        silent: bool = True,
        accept_agreements: bool = True
    ) -> OperationStatus:
        """Install a new package by ID."""
        pass

    @abc.abstractmethod
    def uninstall_package(
        self, 
        package_id: str, 
        on_log: Optional[Callable[[str], None]] = None
    ) -> OperationStatus:
        """Uninstall an existing package."""
        pass

    @abc.abstractmethod
    def abort(self):
        """Immediately abort and terminate any active background subprocess."""
        pass
