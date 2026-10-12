// Hand-picked starter catalog. Every id was verified with `winget show --id <id> --exact` (2026-10-12).
// P2 replaces this with the curated Rust catalog (categories, bundles, Chocolatey mappings).
export interface PopularApp {
  id: string;
  name: string;
  publisher: string;
  blurb: string;
  category: 'Browsers' | 'Developer' | 'Media' | 'Communication' | 'Utilities' | 'Productivity' | 'Gaming' | 'Security';
}

export const POPULAR: PopularApp[] = [
  { id: 'Mozilla.Firefox', name: 'Firefox', publisher: 'Mozilla', blurb: 'Fast, private browsing from a non-profit.', category: 'Browsers' },
  { id: 'Google.Chrome', name: 'Google Chrome', publisher: 'Google', blurb: 'The most widely used web browser.', category: 'Browsers' },
  { id: 'Brave.Brave', name: 'Brave', publisher: 'Brave Software', blurb: 'Blocks ads and trackers by default.', category: 'Browsers' },
  { id: 'Microsoft.VisualStudioCode', name: 'Visual Studio Code', publisher: 'Microsoft', blurb: 'A light, extensible code editor.', category: 'Developer' },
  { id: 'Git.Git', name: 'Git', publisher: 'Git community', blurb: 'Version control for every project.', category: 'Developer' },
  { id: 'Python.Python.3.13', name: 'Python 3.13', publisher: 'Python Software Foundation', blurb: 'Readable, batteries-included programming.', category: 'Developer' },
  { id: 'OpenJS.NodeJS.LTS', name: 'Node.js LTS', publisher: 'OpenJS Foundation', blurb: 'JavaScript runtime, long-term support.', category: 'Developer' },
  { id: 'Microsoft.WindowsTerminal', name: 'Windows Terminal', publisher: 'Microsoft', blurb: 'Tabs, profiles and a modern console.', category: 'Developer' },
  { id: 'Spotify.Spotify', name: 'Spotify', publisher: 'Spotify', blurb: 'Music and podcasts, everywhere.', category: 'Media' },
  { id: 'VideoLAN.VLC', name: 'VLC media player', publisher: 'VideoLAN', blurb: 'Plays almost any video or audio file.', category: 'Media' },
  { id: 'OBSProject.OBSStudio', name: 'OBS Studio', publisher: 'OBS Project', blurb: 'Record and stream your screen.', category: 'Media' },
  { id: 'Audacity.Audacity', name: 'Audacity', publisher: 'Audacity', blurb: 'Record and edit audio, free.', category: 'Media' },
  { id: 'GIMP.GIMP.3', name: 'GIMP', publisher: 'The GIMP Team', blurb: 'Serious photo editing without a subscription.', category: 'Media' },
  { id: 'Discord.Discord', name: 'Discord', publisher: 'Discord', blurb: 'Voice, video and chat for communities.', category: 'Communication' },
  { id: 'Zoom.Zoom', name: 'Zoom Workplace', publisher: 'Zoom', blurb: 'Meetings, chat and calls.', category: 'Communication' },
  { id: 'SlackTechnologies.Slack', name: 'Slack', publisher: 'Slack Technologies', blurb: 'Where work conversations happen.', category: 'Communication' },
  { id: '7zip.7zip', name: '7-Zip', publisher: 'Igor Pavlov', blurb: 'Open and create any archive.', category: 'Utilities' },
  { id: 'Microsoft.PowerToys', name: 'PowerToys', publisher: 'Microsoft', blurb: 'Power-user tools for Windows.', category: 'Utilities' },
  { id: 'voidtools.Everything', name: 'Everything', publisher: 'voidtools', blurb: 'Find any file by name, instantly.', category: 'Utilities' },
  { id: 'ShareX.ShareX', name: 'ShareX', publisher: 'ShareX Team', blurb: 'Screenshots and recordings, with annotations.', category: 'Utilities' },
  { id: 'Notepad++.Notepad++', name: 'Notepad++', publisher: 'Notepad++ Team', blurb: 'A quick, capable text editor.', category: 'Productivity' },
  { id: 'Obsidian.Obsidian', name: 'Obsidian', publisher: 'Obsidian', blurb: 'Private notes that link together.', category: 'Productivity' },
  { id: 'Valve.Steam', name: 'Steam', publisher: 'Valve', blurb: 'Your PC games library.', category: 'Gaming' },
  { id: 'Bitwarden.Bitwarden', name: 'Bitwarden', publisher: 'Bitwarden', blurb: 'Open-source password manager.', category: 'Security' },
];
