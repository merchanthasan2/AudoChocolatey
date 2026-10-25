/**
 * Quantum Updater - Modern Glassmorphic Software & Package Manager
 * Developed by Quantum Step (https://quantumstep.in)
 */

const MONOGRAM_PALETTES = [
  ['#108AB1', '#073A4B'], // Ocean Blue & Midnight Teal
  ['#06D7A0', '#073A4B'], // Emerald Mint & Deep Teal
  ['#F04770', '#8c1f38'], // Coral Pink
  ['#FFD167', '#073A4B'], // Golden Yellow & Deep Teal
  ['#F78C6A', '#073A4B'], // Coral Peach & Deep Teal
  ['#108AB1', '#06D7A0'], // Ocean Blue & Emerald Mint
  ['#F04770', '#F78C6A'], // Coral Pink & Coral Peach
  ['#FFD167', '#F78C6A'], // Golden Yellow & Coral Peach
  ['#06D7A0', '#108AB1']  // Emerald Mint & Ocean Blue
];

// Direct package ID to official SVG logo mapping (Simple Icons CDN)
const APP_LOGOS_MAP = {
  // Artificial Intelligence & Machine Learning
  'ollama.ollama': { slug: 'ollama', bg: 'rgba(255, 255, 255, 0.12)' },
  'elementlabs.lmstudio': { slug: 'openai', bg: 'rgba(99, 102, 241, 0.15)' },
  'jan.jan': { slug: 'openai', bg: 'rgba(16, 185, 129, 0.15)' },
  'openai.chatgpt': { slug: 'openai', bg: 'rgba(16, 163, 127, 0.15)' },
  'anthropic.claude': { slug: 'anthropic', bg: 'rgba(204, 120, 92, 0.15)' },
  'anysphere.cursor': { slug: 'visualstudiocode', bg: 'rgba(59, 130, 246, 0.15)' },
  'superwhisper.superwhisper': { slug: 'openai', bg: 'rgba(236, 72, 153, 0.15)' },
  'upscayl.upscayl': { slug: 'nvidia', bg: 'rgba(118, 185, 0, 0.15)' },
  'pinokio.pinokio': { slug: 'github', bg: 'rgba(139, 92, 246, 0.15)' },

  // Browsers
  'google.chrome': { slug: 'googlechrome', bg: 'rgba(66, 133, 244, 0.12)' },
  'mozilla.firefox': { slug: 'firefox', bg: 'rgba(255, 113, 57, 0.12)' },
  'brave.brave': { slug: 'brave', bg: 'rgba(251, 84, 43, 0.12)' },
  'microsoft.edge': { slug: 'microsoftedge', bg: 'rgba(0, 120, 215, 0.12)' },
  'opera.opera': { slug: 'opera', bg: 'rgba(255, 27, 45, 0.12)' },
  'torproject.torbrowser': { slug: 'torbrowser', bg: 'rgba(125, 70, 152, 0.12)' },
  'vivaldi.vivaldi': { slug: 'vivaldi', bg: 'rgba(239, 57, 57, 0.12)' },

  // Development
  'microsoft.visualstudiocode': { slug: 'visualstudiocode', bg: 'rgba(0, 122, 204, 0.12)' },
  'git.git': { slug: 'git', bg: 'rgba(240, 80, 50, 0.12)' },
  'github.githubdesktop': { slug: 'github', bg: 'rgba(255, 255, 255, 0.12)' },
  'docker.dockerdesktop': { slug: 'docker', bg: 'rgba(36, 150, 237, 0.12)' },
  'postman.postman': { slug: 'postman', bg: 'rgba(255, 108, 55, 0.12)' },
  'insomnia.insomnia': { slug: 'insomnia', bg: 'rgba(88, 73, 190, 0.12)' },
  'dbeaver.dbeaver': { slug: 'dbeaver', bg: 'rgba(56, 41, 35, 0.15)' },
  'python.python.3.13': { slug: 'python', bg: 'rgba(55, 118, 171, 0.12)' },
  'python.python.3': { slug: 'python', bg: 'rgba(55, 118, 171, 0.12)' },
  'python.python': { slug: 'python', bg: 'rgba(55, 118, 171, 0.12)' },
  'openjs.nodejs.lts': { slug: 'nodedotjs', bg: 'rgba(95, 160, 78, 0.12)' },
  'openjs.nodejs': { slug: 'nodedotjs', bg: 'rgba(95, 160, 78, 0.12)' },
  'rustlang.rustup': { slug: 'rust', bg: 'rgba(255, 255, 255, 0.1)' },
  'golang.go': { slug: 'go', bg: 'rgba(0, 173, 216, 0.12)' },
  'microsoft.windowsterminal': { slug: 'windowsterminal', bg: 'rgba(77, 77, 77, 0.15)' },
  'wiresharkfoundation.wireshark': { slug: 'wireshark', bg: 'rgba(22, 121, 167, 0.12)' },

  // Music & Media
  'spotify.spotify': { slug: 'spotify', bg: 'rgba(29, 185, 84, 0.12)' },
  'videolan.vlc': { slug: 'vlcmediaplayer', bg: 'rgba(255, 136, 0, 0.15)' },
  'audacity.audacity': { slug: 'audacity', bg: 'rgba(0, 0, 235, 0.12)' },
  'obsproject.obsstudio': { slug: 'obsstudio', bg: 'rgba(48, 44, 52, 0.15)' },
  'handbrake.handbrake': { slug: 'handbrake', bg: 'rgba(229, 57, 53, 0.12)' },
  'foobar2000.foobar2000': { slug: 'foobar2000', bg: 'rgba(103, 58, 183, 0.12)' },
  'plex.plex': { slug: 'plex', bg: 'rgba(229, 160, 13, 0.12)' },
  'kodi.kodi': { slug: 'kodi', bg: 'rgba(23, 178, 231, 0.12)' },
  'deezer.deezer': { slug: 'deezer', bg: 'rgba(162, 56, 255, 0.12)' },

  // Addons & Utilities
  '7zip.7zip': { slug: 'sevenzip', bg: 'rgba(255, 255, 255, 0.1)' },
  'rarlab.winrar': { slug: 'winrar', bg: 'rgba(63, 81, 181, 0.12)' },
  'microsoft.powertoys': { slug: 'windows', bg: 'rgba(0, 120, 212, 0.12)' },
  'autohotkey.autohotkey': { slug: 'autohotkey', bg: 'rgba(51, 68, 85, 0.15)' },
  'bitwarden.bitwarden': { slug: 'bitwarden', bg: 'rgba(23, 93, 220, 0.12)' },
  'keepassxcteam.keepassxc': { slug: 'keepassxc', bg: 'rgba(72, 160, 72, 0.12)' },
  'qbittorrent.qbittorrent': { slug: 'qbittorrent', bg: 'rgba(47, 103, 159, 0.12)' },
  'bleachbit.bleachbit': { slug: 'bleachbit', bg: 'rgba(76, 175, 80, 0.12)' },
  'anydesksoftwaregmbh.anydesk': { slug: 'anydesk', bg: 'rgba(239, 68, 59, 0.12)' },

  // Productivity
  'notion.notion': { slug: 'notion', bg: 'rgba(255, 255, 255, 0.1)' },
  'obsidian.obsidian': { slug: 'obsidian', bg: 'rgba(124, 58, 237, 0.12)' },
  'notepad++.notepad++': { slug: 'notepadplusplus', bg: 'rgba(144, 197, 63, 0.15)' },
  'thedocumentfoundation.libreoffice': { slug: 'libreoffice', bg: 'rgba(24, 163, 3, 0.12)' },
  'adobe.acrobat.reader.64-bit': { slug: 'adobeacrobatreader', bg: 'rgba(236, 28, 36, 0.12)' },
  'figma.figma': { slug: 'figma', bg: 'rgba(242, 78, 30, 0.12)' },
  'blenderfoundation.blender': { slug: 'blender', bg: 'rgba(232, 125, 13, 0.12)' },
  'gimp.gimp': { slug: 'gimp', bg: 'rgba(92, 85, 67, 0.15)' },
  'inkscape.inkscape': { slug: 'inkscape', bg: 'rgba(255, 255, 255, 0.1)' },

  // Gaming & Social
  'discord.discord': { slug: 'discord', bg: 'rgba(88, 101, 242, 0.15)' },
  'telegram.telegramdesktop': { slug: 'telegram', bg: 'rgba(38, 165, 228, 0.12)' },
  'whatsapp.whatsapp': { slug: 'whatsapp', bg: 'rgba(37, 211, 102, 0.12)' },
  'slacktechnologies.slack': { slug: 'slack', bg: 'rgba(74, 21, 75, 0.15)' },
  'zoom.zoom': { slug: 'zoom', bg: 'rgba(11, 92, 255, 0.12)' },
  'microsoft.teams': { slug: 'microsoftteams', bg: 'rgba(98, 100, 167, 0.12)' },
  'valve.steam': { slug: 'steam', bg: 'rgba(255, 255, 255, 0.1)' },
  'epicgames.epicgameslauncher': { slug: 'epicgames', bg: 'rgba(49, 49, 49, 0.15)' },
  'gog.galaxy': { slug: 'gogdotcom', bg: 'rgba(138, 43, 226, 0.12)' }
};

// Substring / keyword logo fallbacks
const APP_KEYWORD_LOGOS = {
  'ollama': { slug: 'ollama', bg: 'rgba(255, 255, 255, 0.12)' },
  'lmstudio': { slug: 'openai', bg: 'rgba(99, 102, 241, 0.15)' },
  'jan': { slug: 'openai', bg: 'rgba(16, 185, 129, 0.15)' },
  'chatgpt': { slug: 'openai', bg: 'rgba(16, 163, 127, 0.15)' },
  'claude': { slug: 'anthropic', bg: 'rgba(204, 120, 92, 0.15)' },
  'cursor': { slug: 'visualstudiocode', bg: 'rgba(59, 130, 246, 0.15)' },
  'superwhisper': { slug: 'openai', bg: 'rgba(236, 72, 153, 0.15)' },
  'upscayl': { slug: 'nvidia', bg: 'rgba(118, 185, 0, 0.15)' },
  'pinokio': { slug: 'github', bg: 'rgba(139, 92, 246, 0.15)' },

  'chrome': { slug: 'googlechrome', bg: 'rgba(66, 133, 244, 0.12)' },
  'firefox': { slug: 'firefox', bg: 'rgba(255, 113, 57, 0.12)' },
  'visualstudio': { slug: 'visualstudio', bg: 'rgba(92, 45, 145, 0.12)' },
  'vscode': { slug: 'visualstudiocode', bg: 'rgba(0, 122, 204, 0.12)' },
  'code': { slug: 'visualstudiocode', bg: 'rgba(0, 122, 204, 0.12)' },
  'vlc': { slug: 'vlcmediaplayer', bg: 'rgba(255, 136, 0, 0.15)' },
  'spotify': { slug: 'spotify', bg: 'rgba(29, 185, 84, 0.12)' },
  'git': { slug: 'git', bg: 'rgba(240, 80, 50, 0.12)' },
  'docker': { slug: 'docker', bg: 'rgba(36, 150, 237, 0.12)' },
  'python': { slug: 'python', bg: 'rgba(55, 118, 171, 0.12)' },
  'node': { slug: 'nodedotjs', bg: 'rgba(95, 160, 78, 0.12)' },
  'discord': { slug: 'discord', bg: 'rgba(88, 101, 242, 0.15)' },
  'slack': { slug: 'slack', bg: 'rgba(74, 21, 75, 0.15)' },
  'telegram': { slug: 'telegram', bg: 'rgba(38, 165, 228, 0.12)' },
  'whatsapp': { slug: 'whatsapp', bg: 'rgba(37, 211, 102, 0.12)' },
  'zoom': { slug: 'zoom', bg: 'rgba(11, 92, 255, 0.12)' },
  'steam': { slug: 'steam', bg: 'rgba(255, 255, 255, 0.1)' },
  'epic': { slug: 'epicgames', bg: 'rgba(49, 49, 49, 0.15)' },
  'notepad++': { slug: 'notepadplusplus', bg: 'rgba(144, 197, 63, 0.15)' },
  '7zip': { slug: 'sevenzip', bg: 'rgba(255, 255, 255, 0.1)' },
  'winrar': { slug: 'winrar', bg: 'rgba(63, 81, 181, 0.12)' },
  'postman': { slug: 'postman', bg: 'rgba(255, 108, 55, 0.12)' },
  'obs': { slug: 'obsstudio', bg: 'rgba(48, 44, 52, 0.15)' },
  'audacity': { slug: 'audacity', bg: 'rgba(0, 0, 235, 0.12)' },
  'blender': { slug: 'blender', bg: 'rgba(232, 125, 13, 0.12)' },
  'gimp': { slug: 'gimp', bg: 'rgba(92, 85, 67, 0.15)' },
  'inkscape': { slug: 'inkscape', bg: 'rgba(255, 255, 255, 0.1)' },
  'figma': { slug: 'figma', bg: 'rgba(242, 78, 30, 0.12)' },
  'notion': { slug: 'notion', bg: 'rgba(255, 255, 255, 0.1)' },
  'obsidian': { slug: 'obsidian', bg: 'rgba(124, 58, 237, 0.12)' },
  'libreoffice': { slug: 'libreoffice', bg: 'rgba(24, 163, 3, 0.12)' },
  'bitwarden': { slug: 'bitwarden', bg: 'rgba(23, 93, 220, 0.12)' },
  'keepass': { slug: 'keepassxc', bg: 'rgba(72, 160, 72, 0.12)' },
  'wireshark': { slug: 'wireshark', bg: 'rgba(22, 121, 167, 0.12)' },
  'rust': { slug: 'rust', bg: 'rgba(255, 255, 255, 0.1)' },
  'golang': { slug: 'go', bg: 'rgba(0, 173, 216, 0.12)' },
  'edge': { slug: 'microsoftedge', bg: 'rgba(0, 120, 215, 0.12)' },
  'brave': { slug: 'brave', bg: 'rgba(251, 84, 43, 0.12)' },
  'opera': { slug: 'opera', bg: 'rgba(255, 27, 45, 0.12)' },
  'tor': { slug: 'torbrowser', bg: 'rgba(125, 70, 152, 0.12)' },
  'qbittorrent': { slug: 'qbittorrent', bg: 'rgba(47, 103, 159, 0.12)' },
  'anydesk': { slug: 'anydesk', bg: 'rgba(239, 68, 59, 0.12)' },
  'powertoys': { slug: 'windows', bg: 'rgba(0, 120, 212, 0.12)' },
  'autohotkey': { slug: 'autohotkey', bg: 'rgba(51, 68, 85, 0.15)' },
  'dbeaver': { slug: 'dbeaver', bg: 'rgba(56, 41, 35, 0.15)' },
  'insomnia': { slug: 'insomnia', bg: 'rgba(88, 73, 190, 0.12)' },
  'handbrake': { slug: 'handbrake', bg: 'rgba(229, 57, 53, 0.12)' },
  'plex': { slug: 'plex', bg: 'rgba(229, 160, 13, 0.12)' },
  'kodi': { slug: 'kodi', bg: 'rgba(23, 178, 231, 0.12)' },
  'deezer': { slug: 'deezer', bg: 'rgba(162, 56, 255, 0.12)' },
  'acrobat': { slug: 'adobeacrobatreader', bg: 'rgba(236, 28, 36, 0.12)' },
  'microsoft': { slug: 'windows', bg: 'rgba(0, 120, 212, 0.12)' },
  'google': { slug: 'google', bg: 'rgba(66, 133, 244, 0.12)' },
  'apple': { slug: 'apple', bg: 'rgba(255, 255, 255, 0.1)' }
};

// Rich Curated Explore Catalog (Automatically loaded on launch)
const APP_CATALOG = [
  // Artificial Intelligence & Local LLMs
  {
    id: 'Ollama.Ollama',
    name: 'Ollama',
    category: 'ai',
    desc: 'Run large language models locally (Llama 3, DeepSeek, Mistral, Gemma) on CPU and GPU with zero cloud lock-in.',
    downloads: 99,
    version: '0.5.7',
    logo: 'https://cdn.simpleicons.org/ollama',
    bg: 'rgba(255, 255, 255, 0.12)'
  },
  {
    id: 'ElementLabs.LMStudio',
    name: 'LM Studio',
    category: 'ai',
    desc: 'Discover, download, and run local LLMs with GPU acceleration, interactive chat UI, and OpenAI-compatible API server.',
    downloads: 98,
    version: '0.3.6',
    logo: 'https://cdn.simpleicons.org/openai',
    bg: 'rgba(99, 102, 241, 0.15)'
  },
  {
    id: 'Jan.Jan',
    name: 'Jan AI',
    category: 'ai',
    desc: 'Self-hosted, open-source ChatGPT alternative that runs 100% offline on your device with complete privacy.',
    downloads: 95,
    version: '0.5.10',
    logo: 'https://cdn.simpleicons.org/openai',
    bg: 'rgba(16, 185, 129, 0.15)'
  },
  {
    id: 'OpenAI.ChatGPT',
    name: 'ChatGPT Desktop',
    category: 'ai',
    desc: 'Official Windows desktop client for OpenAI ChatGPT with instant voice mode, global shortcuts, and file attachments.',
    downloads: 100,
    version: '1.2024.310',
    logo: 'https://cdn.simpleicons.org/openai',
    bg: 'rgba(16, 163, 127, 0.15)'
  },
  {
    id: 'Anthropic.Claude',
    name: 'Claude Desktop',
    category: 'ai',
    desc: 'Official Anthropic Claude desktop application with Model Context Protocol (MCP) and Artifacts rendering.',
    downloads: 97,
    version: '0.7.8',
    logo: 'https://cdn.simpleicons.org/anthropic',
    bg: 'rgba(204, 120, 92, 0.15)'
  },
  {
    id: 'Anysphere.Cursor',
    name: 'Cursor AI Editor',
    category: 'ai',
    desc: 'AI-first code editor fork of VS Code with multi-file reasoning, inline diffs, and terminal execution.',
    downloads: 99,
    version: '0.45.6',
    logo: 'https://cdn.simpleicons.org/visualstudiocode',
    bg: 'rgba(59, 130, 246, 0.15)'
  },
  {
    id: 'Superwhisper.Superwhisper',
    name: 'Superwhisper',
    category: 'ai',
    desc: 'Ultra-fast, accurate offline voice-to-text dictation and transcription powered by local OpenAI Whisper models.',
    downloads: 91,
    version: '1.4.0',
    logo: 'https://cdn.simpleicons.org/openai',
    bg: 'rgba(236, 72, 153, 0.15)'
  },
  {
    id: 'Upscayl.Upscayl',
    name: 'Upscayl',
    category: 'ai',
    desc: 'Free, open source AI image upscaler powered by advanced neural network models with zero quality loss.',
    downloads: 93,
    version: '2.15.0',
    logo: 'https://cdn.simpleicons.org/nvidia',
    bg: 'rgba(118, 185, 0, 0.15)'
  },
  {
    id: 'Pinokio.Pinokio',
    name: 'Pinokio AI Browser',
    category: 'ai',
    desc: 'Autonomous browser ecosystem that lets you install, run, and automate local AI apps and models with 1 click.',
    downloads: 92,
    version: '3.0.0',
    logo: 'https://cdn.simpleicons.org/github/white',
    bg: 'rgba(139, 92, 246, 0.15)'
  },

  // Music & Media
  {
    id: 'Spotify.Spotify',
    name: 'Spotify',
    category: 'media',
    desc: 'Stream millions of tracks, albums, podcasts, and curated playlists.',
    downloads: 99,
    version: '1.2.53',
    logo: 'https://cdn.simpleicons.org/spotify.svg',
    bg: 'rgba(29, 185, 84, 0.15)'
  },
  {
    id: 'VideoLAN.VLC',
    name: 'VLC Media Player',
    category: 'media',
    desc: 'Plays virtually all video and audio codecs, discs, streams, and webcams.',
    downloads: 98,
    version: '3.0.21',
    logo: 'https://cdn.simpleicons.org/vlcmediaplayer.svg',
    bg: 'rgba(255, 136, 0, 0.15)'
  },
  {
    id: 'Audacity.Audacity',
    name: 'Audacity',
    category: 'media',
    desc: 'Open-source multi-track audio editor, recorder, and sound analyzer.',
    downloads: 88,
    version: '3.7.1',
    logo: 'https://cdn.simpleicons.org/audacity.svg',
    bg: 'rgba(0, 0, 235, 0.15)'
  },
  {
    id: 'OBSProject.OBSStudio',
    name: 'OBS Studio',
    category: 'media',
    desc: 'High performance video recording and live streaming for content creators.',
    downloads: 95,
    version: '31.0.0',
    logo: 'https://cdn.simpleicons.org/obsstudio.svg',
    bg: 'rgba(48, 44, 52, 0.2)'
  },
  {
    id: 'HandBrake.HandBrake',
    name: 'HandBrake',
    category: 'media',
    desc: 'Open-source video transcoder for converting video to modern codecs.',
    downloads: 84,
    version: '1.8.2',
    logo: 'https://cdn.simpleicons.org/handbrake.svg',
    bg: 'rgba(229, 57, 53, 0.15)'
  },
  {
    id: 'Foobar2000.Foobar2000',
    name: 'foobar2000',
    category: 'media',
    desc: 'Advanced, lightweight audio player with gapless playback and DSP.',
    downloads: 80,
    version: '2.1.6',
    logo: 'https://cdn.simpleicons.org/foobar2000.svg',
    bg: 'rgba(103, 58, 183, 0.15)'
  },
  {
    id: 'Plex.Plex',
    name: 'Plex Desktop',
    category: 'media',
    desc: 'Stream live TV, movies, shows, and your personal home media library.',
    downloads: 86,
    version: '1.104.0',
    logo: 'https://cdn.simpleicons.org/plex.svg',
    bg: 'rgba(229, 160, 13, 0.15)'
  },
  {
    id: 'Kodi.Kodi',
    name: 'Kodi',
    category: 'media',
    desc: 'Award-winning open source home entertainment hub and digital jukebox.',
    downloads: 83,
    version: '21.1',
    logo: 'https://cdn.simpleicons.org/kodi.svg',
    bg: 'rgba(23, 178, 231, 0.15)'
  },
  {
    id: 'Deezer.Deezer',
    name: 'Deezer',
    category: 'media',
    desc: 'High Fidelity music streaming with FLAC sound and personalized Flow.',
    downloads: 78,
    version: '6.0.190',
    logo: 'https://cdn.simpleicons.org/deezer.svg',
    bg: 'rgba(162, 56, 255, 0.15)'
  },

  // Addons & Utilities
  {
    id: '7zip.7zip',
    name: '7-Zip',
    category: 'utilities',
    desc: 'High compression ratio file archiver supporting 7z, ZIP, RAR, TAR, and ISO.',
    downloads: 99,
    version: '24.09',
    logo: 'https://cdn.simpleicons.org/sevenzip.svg',
    bg: 'rgba(255, 255, 255, 0.1)'
  },
  {
    id: 'RARLab.WinRAR',
    name: 'WinRAR',
    category: 'utilities',
    desc: 'Industry standard RAR archive creator and decompression utility.',
    downloads: 93,
    version: '7.01',
    logo: 'https://cdn.simpleicons.org/winrar.svg',
    bg: 'rgba(63, 81, 181, 0.15)'
  },
  {
    id: 'Microsoft.PowerToys',
    name: 'Microsoft PowerToys',
    category: 'utilities',
    desc: 'Essential Windows system utilities for power users: FancyZones, Text Extractor, and Run.',
    downloads: 97,
    version: '0.86.0',
    logo: 'https://cdn.simpleicons.org/windows.svg',
    bg: 'rgba(0, 120, 212, 0.15)'
  },
  {
    id: 'voidtools.Everything',
    name: 'Everything',
    category: 'utilities',
    desc: 'Locate files and folders across all NTFS drives in milliseconds.',
    downloads: 96,
    version: '1.4.1',
    logo: 'https://www.voidtools.com/favicon.ico',
    bg: 'rgba(255, 152, 0, 0.15)'
  },
  {
    id: 'Rufus.Rufus',
    name: 'Rufus',
    category: 'utilities',
    desc: 'Reliably create bootable USB drives for Windows, Linux, and UEFI installations.',
    downloads: 92,
    version: '4.6',
    logo: 'https://rufus.ie/pics/rufus-128.png',
    bg: 'rgba(158, 158, 158, 0.15)'
  },
  {
    id: 'AutoHotkey.AutoHotkey',
    name: 'AutoHotkey',
    category: 'utilities',
    desc: 'Desktop automation and macro scripting language for custom keybindings.',
    downloads: 87,
    version: '2.0.18',
    logo: 'https://cdn.simpleicons.org/autohotkey.svg',
    bg: 'rgba(51, 68, 85, 0.2)'
  },
  {
    id: 'Bitwarden.Bitwarden',
    name: 'Bitwarden',
    category: 'utilities',
    desc: 'Open-source end-to-end encrypted password manager and passkey vault.',
    downloads: 94,
    version: '2024.11.0',
    logo: 'https://cdn.simpleicons.org/bitwarden.svg',
    bg: 'rgba(23, 93, 220, 0.15)'
  },
  {
    id: 'KeePassXCTeam.KeePassXC',
    name: 'KeePassXC',
    category: 'utilities',
    desc: 'Offline encrypted password manager with browser integration and auto-type.',
    downloads: 85,
    version: '2.7.9',
    logo: 'https://cdn.simpleicons.org/keepassxc.svg',
    bg: 'rgba(72, 160, 72, 0.15)'
  },
  {
    id: 'qBittorrent.qBittorrent',
    name: 'qBittorrent',
    category: 'utilities',
    desc: 'Free, open-source, ad-free torrent downloader with integrated search.',
    downloads: 91,
    version: '5.0.1',
    logo: 'https://cdn.simpleicons.org/qbittorrent.svg',
    bg: 'rgba(47, 103, 159, 0.15)'
  },
  {
    id: 'BleachBit.BleachBit',
    name: 'BleachBit',
    category: 'utilities',
    desc: 'Free cache cleaner, privacy cleaner, and disk space optimizer.',
    downloads: 81,
    version: '4.6.0',
    logo: 'https://cdn.simpleicons.org/bleachbit.svg',
    bg: 'rgba(76, 175, 80, 0.15)'
  },
  {
    id: 'AnyDeskSoftwareGmbH.AnyDesk',
    name: 'AnyDesk',
    category: 'utilities',
    desc: 'High frame-rate remote desktop application for IT support and telework.',
    downloads: 90,
    version: '8.1.1',
    logo: 'https://cdn.simpleicons.org/anydesk.svg',
    bg: 'rgba(239, 68, 59, 0.15)'
  },

  // Development & Tools
  {
    id: 'Microsoft.VisualStudioCode',
    name: 'Visual Studio Code',
    category: 'development',
    desc: 'Lightweight, extensible code editor with integrated Git, debugging, and terminal.',
    downloads: 100,
    version: '1.95.3',
    logo: 'https://cdn.simpleicons.org/visualstudiocode.svg',
    bg: 'rgba(0, 122, 204, 0.15)'
  },
  {
    id: 'Git.Git',
    name: 'Git',
    category: 'development',
    desc: 'Fast, distributed version control system for tracking source code changes.',
    downloads: 100,
    version: '2.47.1',
    logo: 'https://cdn.simpleicons.org/git.svg',
    bg: 'rgba(240, 80, 50, 0.15)'
  },
  {
    id: 'GitHub.GitHubDesktop',
    name: 'GitHub Desktop',
    category: 'development',
    desc: 'Visual interface for Git and GitHub with seamless diffs, branches, and PRs.',
    downloads: 95,
    version: '3.4.9',
    logo: 'https://cdn.simpleicons.org/github.svg',
    bg: 'rgba(255, 255, 255, 0.1)'
  },
  {
    id: 'Docker.DockerDesktop',
    name: 'Docker Desktop',
    category: 'development',
    desc: 'Build, share, and run containerized microservices with Docker and Kubernetes.',
    downloads: 97,
    version: '4.35.1',
    logo: 'https://cdn.simpleicons.org/docker.svg',
    bg: 'rgba(36, 150, 237, 0.15)'
  },
  {
    id: 'Postman.Postman',
    name: 'Postman',
    category: 'development',
    desc: 'API testing platform for building, debugging, and mocking REST and GraphQL APIs.',
    downloads: 93,
    version: '11.19.0',
    logo: 'https://cdn.simpleicons.org/postman.svg',
    bg: 'rgba(255, 108, 55, 0.15)'
  },
  {
    id: 'Insomnia.Insomnia',
    name: 'Insomnia',
    category: 'development',
    desc: 'Streamlined desktop API client for REST, GraphQL, gRPC, and WebSockets.',
    downloads: 82,
    version: '10.1.1',
    logo: 'https://cdn.simpleicons.org/insomnia.svg',
    bg: 'rgba(88, 73, 190, 0.15)'
  },
  {
    id: 'dbeaver.dbeaver',
    name: 'DBeaver Community',
    category: 'development',
    desc: 'Universal SQL database administration tool for Postgres, MySQL, SQLite, and Oracle.',
    downloads: 89,
    version: '24.2.4',
    logo: 'https://cdn.simpleicons.org/dbeaver.svg',
    bg: 'rgba(56, 41, 35, 0.2)'
  },
  {
    id: 'Python.Python.3.13',
    name: 'Python 3.13',
    category: 'development',
    desc: 'Modern general-purpose programming language for AI, data science, and scripting.',
    downloads: 98,
    version: '3.13.0',
    logo: 'https://cdn.simpleicons.org/python.svg',
    bg: 'rgba(55, 118, 171, 0.15)'
  },
  {
    id: 'OpenJS.NodeJS.LTS',
    name: 'Node.js (LTS)',
    category: 'development',
    desc: 'JavaScript runtime built on Chrome V8 engine for scalable backend servers.',
    downloads: 97,
    version: '22.11.0',
    logo: 'https://cdn.simpleicons.org/nodedotjs.svg',
    bg: 'rgba(95, 160, 78, 0.15)'
  },
  {
    id: 'Rustlang.Rustup',
    name: 'Rust (Rustup)',
    category: 'development',
    desc: 'Official installer for the memory-safe, ultra-fast Rust programming language.',
    downloads: 90,
    version: '1.27.1',
    logo: 'https://cdn.simpleicons.org/rust.svg',
    bg: 'rgba(255, 255, 255, 0.1)'
  },
  {
    id: 'Golang.Go',
    name: 'Go Programming Language',
    category: 'development',
    desc: 'Simple, fast, and concurrent systems programming language developed by Google.',
    downloads: 88,
    version: '1.23.3',
    logo: 'https://cdn.simpleicons.org/go.svg',
    bg: 'rgba(0, 173, 216, 0.15)'
  },
  {
    id: 'Microsoft.WindowsTerminal',
    name: 'Windows Terminal',
    category: 'development',
    desc: 'GPU-accelerated terminal for PowerShell, Command Prompt, and WSL Linux.',
    downloads: 96,
    version: '1.21.2911',
    logo: 'https://cdn.simpleicons.org/windowsterminal.svg',
    bg: 'rgba(77, 77, 77, 0.2)'
  },
  {
    id: 'WiresharkFoundation.Wireshark',
    name: 'Wireshark',
    category: 'development',
    desc: 'World’s foremost network traffic and packet capture analyzer.',
    downloads: 87,
    version: '4.4.1',
    logo: 'https://cdn.simpleicons.org/wireshark.svg',
    bg: 'rgba(22, 121, 167, 0.15)'
  },

  // Browsers & Web
  {
    id: 'Google.Chrome',
    name: 'Google Chrome',
    category: 'browsers',
    desc: 'Fast, secure, and customizable browser built by Google for the modern web.',
    downloads: 100,
    version: '131.0.6778',
    logo: 'https://cdn.simpleicons.org/googlechrome.svg',
    bg: 'rgba(66, 133, 244, 0.15)'
  },
  {
    id: 'Mozilla.Firefox',
    name: 'Mozilla Firefox',
    category: 'browsers',
    desc: 'Privacy-focused, independent web browser with advanced tracking protection.',
    downloads: 96,
    version: '132.0.2',
    logo: 'https://cdn.simpleicons.org/firefox.svg',
    bg: 'rgba(255, 113, 57, 0.15)'
  },
  {
    id: 'Brave.Brave',
    name: 'Brave Browser',
    category: 'browsers',
    desc: 'Shields privacy by blocking advertisements and trackers automatically by default.',
    downloads: 95,
    version: '1.71.123',
    logo: 'https://cdn.simpleicons.org/brave.svg',
    bg: 'rgba(251, 84, 43, 0.15)'
  },
  {
    id: 'Microsoft.Edge',
    name: 'Microsoft Edge',
    category: 'browsers',
    desc: 'Fast and secure Windows browser with built-in Copilot AI features and vertical tabs.',
    downloads: 92,
    version: '131.0.2903',
    logo: 'https://cdn.simpleicons.org/microsoftedge.svg',
    bg: 'rgba(0, 120, 215, 0.15)'
  },
  {
    id: 'Opera.Opera',
    name: 'Opera',
    category: 'browsers',
    desc: 'Feature-rich web browser with built-in free VPN, ad blocker, and AI assistant.',
    downloads: 87,
    version: '114.0.5282',
    logo: 'https://cdn.simpleicons.org/opera.svg',
    bg: 'rgba(255, 27, 45, 0.15)'
  },
  {
    id: 'TorProject.TorBrowser',
    name: 'Tor Browser',
    category: 'browsers',
    desc: 'Browse anonymously with multi-layered onion encryption protecting privacy.',
    downloads: 84,
    version: '14.0.1',
    logo: 'https://cdn.simpleicons.org/torbrowser.svg',
    bg: 'rgba(125, 70, 152, 0.15)'
  },
  {
    id: 'Vivaldi.Vivaldi',
    name: 'Vivaldi',
    category: 'browsers',
    desc: 'Highly customizable power browser with tabs stacking, split views, and notes.',
    downloads: 83,
    version: '7.0.3495',
    logo: 'https://cdn.simpleicons.org/vivaldi.svg',
    bg: 'rgba(239, 57, 57, 0.15)'
  },

  // Productivity & Office
  {
    id: 'Notion.Notion',
    name: 'Notion',
    category: 'productivity',
    desc: 'The all-in-one workspace for notes, tasks, databases, and project management.',
    downloads: 96,
    version: '3.14.0',
    logo: 'https://cdn.simpleicons.org/notion.svg',
    bg: 'rgba(255, 255, 255, 0.1)'
  },
  {
    id: 'Obsidian.Obsidian',
    name: 'Obsidian',
    category: 'productivity',
    desc: 'Markdown-based second brain and personal knowledge base stored locally on disk.',
    downloads: 94,
    version: '1.7.5',
    logo: 'https://cdn.simpleicons.org/obsidian.svg',
    bg: 'rgba(124, 58, 237, 0.15)'
  },
  {
    id: 'Notepad++.Notepad++',
    name: 'Notepad++',
    category: 'productivity',
    desc: 'Fast, lightweight text and source code editor with multi-document tabs.',
    downloads: 98,
    version: '8.7.1',
    logo: 'https://cdn.simpleicons.org/notepadplusplus.svg',
    bg: 'rgba(144, 197, 63, 0.18)'
  },
  {
    id: 'TheDocumentFoundation.LibreOffice',
    name: 'LibreOffice',
    category: 'productivity',
    desc: 'Free open source office suite for documents, spreadsheets, and presentations.',
    downloads: 91,
    version: '24.8.3',
    logo: 'https://cdn.simpleicons.org/libreoffice.svg',
    bg: 'rgba(24, 163, 3, 0.15)'
  },
  {
    id: 'Adobe.Acrobat.Reader.64-bit',
    name: 'Adobe Acrobat Reader',
    category: 'productivity',
    desc: 'Standard for viewing, printing, signing, and annotating PDF documents.',
    downloads: 95,
    version: '24.004.20243',
    logo: 'https://cdn.simpleicons.org/adobeacrobatreader.svg',
    bg: 'rgba(236, 28, 36, 0.15)'
  },
  {
    id: 'Figma.Figma',
    name: 'Figma Desktop',
    category: 'productivity',
    desc: 'Collaborative UI/UX design tool for prototyping and design systems.',
    downloads: 93,
    version: '124.5.5',
    logo: 'https://cdn.simpleicons.org/figma.svg',
    bg: 'rgba(242, 78, 30, 0.15)'
  },
  {
    id: 'BlenderFoundation.Blender',
    name: 'Blender',
    category: 'productivity',
    desc: 'Free and open source 3D creation suite: modeling, animation, VFX, and rendering.',
    downloads: 93,
    version: '4.2.3',
    logo: 'https://cdn.simpleicons.org/blender.svg',
    bg: 'rgba(232, 125, 13, 0.15)'
  },
  {
    id: 'GIMP.GIMP',
    name: 'GIMP',
    category: 'productivity',
    desc: 'Cross-platform image editor with advanced retouching and drawing tools.',
    downloads: 87,
    version: '2.10.38',
    logo: 'https://cdn.simpleicons.org/gimp.svg',
    bg: 'rgba(92, 85, 67, 0.2)'
  },
  {
    id: 'Inkscape.Inkscape',
    name: 'Inkscape',
    category: 'productivity',
    desc: 'Professional vector graphics editor for creating SVG illustrations and logos.',
    downloads: 86,
    version: '1.4',
    logo: 'https://cdn.simpleicons.org/inkscape.svg',
    bg: 'rgba(255, 255, 255, 0.1)'
  },

  // Gaming & Social
  {
    id: 'Discord.Discord',
    name: 'Discord',
    category: 'gaming',
    desc: 'High clarity voice, video, and text communication for communities and gamers.',
    downloads: 99,
    version: '1.0.9168',
    logo: 'https://cdn.simpleicons.org/discord.svg',
    bg: 'rgba(88, 101, 242, 0.18)'
  },
  {
    id: 'Telegram.TelegramDesktop',
    name: 'Telegram Desktop',
    category: 'gaming',
    desc: 'Cloud-based instant messaging with powerful channels, bots, and file sharing.',
    downloads: 95,
    version: '5.7.1',
    logo: 'https://cdn.simpleicons.org/telegram.svg',
    bg: 'rgba(38, 165, 228, 0.15)'
  },
  {
    id: 'WhatsApp.WhatsApp',
    name: 'WhatsApp',
    category: 'gaming',
    desc: 'Simple, reliable, private messaging and HD calls synced to your PC.',
    downloads: 97,
    version: '2.2444.6',
    logo: 'https://cdn.simpleicons.org/whatsapp.svg',
    bg: 'rgba(37, 211, 102, 0.15)'
  },
  {
    id: 'SlackTechnologies.Slack',
    name: 'Slack',
    category: 'gaming',
    desc: 'Enterprise communication hub bringing teamwork, channels, and apps together.',
    downloads: 94,
    version: '4.41.97',
    logo: 'https://cdn.simpleicons.org/slack.svg',
    bg: 'rgba(74, 21, 75, 0.18)'
  },
  {
    id: 'Zoom.Zoom',
    name: 'Zoom Workplace',
    category: 'gaming',
    desc: 'Video meetings, virtual team chat, whiteboard, and screen sharing.',
    downloads: 93,
    version: '6.2.6',
    logo: 'https://cdn.simpleicons.org/zoom.svg',
    bg: 'rgba(11, 92, 255, 0.15)'
  },
  {
    id: 'Microsoft.Teams',
    name: 'Microsoft Teams',
    category: 'gaming',
    desc: 'Connect and collaborate with coworkers, group calls, and Office integration.',
    downloads: 92,
    version: '24277.502',
    logo: 'https://cdn.simpleicons.org/microsoftteams.svg',
    bg: 'rgba(98, 100, 167, 0.15)'
  },
  {
    id: 'Valve.Steam',
    name: 'Steam',
    category: 'gaming',
    desc: 'The ultimate digital entertainment platform for playing and purchasing PC games.',
    downloads: 98,
    version: '2.10.91',
    logo: 'https://cdn.simpleicons.org/steam.svg',
    bg: 'rgba(255, 255, 255, 0.1)'
  },
  {
    id: 'EpicGames.EpicGamesLauncher',
    name: 'Epic Games Launcher',
    category: 'gaming',
    desc: 'Play Fortnite, claim free weekly games, and build with Unreal Engine.',
    downloads: 93,
    version: '16.14.1',
    logo: 'https://cdn.simpleicons.org/epicgames.svg',
    bg: 'rgba(49, 49, 49, 0.2)'
  },
  {
    id: 'GOG.Galaxy',
    name: 'GOG Galaxy',
    category: 'gaming',
    desc: 'All your PC games in one launcher with DRM-free downloads and friend lists.',
    downloads: 85,
    version: '2.0.73',
    logo: 'https://cdn.simpleicons.org/gogdotcom.svg',
    bg: 'rgba(138, 43, 226, 0.15)'
  }
];

class QuantumUpdaterApp {
  constructor() {
    this.updates = [];
    this.selectedIds = new Set();
    this.installed = [];
    this.installedSet = new Set();
    this.activeTab = 'updates';
    this.isScanning = false;
    this.isUpdating = false;
    this.activeEngine = 'winget';

    // Explore Filters & Sorting State
    this.exploreCategory = 'all';
    this.exploreSort = 'popular';
    this.exploreSearchTerm = '';

    // Sequential Operation Queue System
    this.operationQueue = [];
    this.isProcessingQueue = false;
    this.currentQueueTask = null;

    // Quantum Clean State
    this.cleanupData = null;
    this.selectedCleanupCategories = new Set();
    this.isCleaning = false;

    // In-App Self-Updater State
    this.pendingUpdate = null;
    this.isCheckingAppUpdate = false;
    this.isApplyingAppUpdate = false;

    this.init();
  }


  async init() {
    this.bindEvents();

    // Render Curated Explore Catalog immediately on startup
    this.renderExplore();

    // Check Tauri v2 Native Bridge
    if (window.__TAURI__) {
      console.log('[Quantum Updater]: Native Tauri v2 Runtime active.');
      if (window.__TAURI__.event) {
        window.__TAURI__.event.listen('progress-update', (event) => {
          const d = event.payload;
          if (d) this.setIslandProgress(d.percent, d.ticker);
        });
        window.__TAURI__.event.listen('log-line', (event) => {
          if (event.payload) this.appendLog(event.payload);
        });
      }
      this.onBridgeReady();
    } else if (window.pywebview) {
      this.onBridgeReady();
    } else {
      window.addEventListener('pywebviewready', () => this.onBridgeReady());
      // Fallback
      setTimeout(() => this.onBridgeReady(), 300);
    }
  }

  async invoke(cmd, args = {}) {
    try {
      if (window.__TAURI__ && window.__TAURI__.core) {
        return await window.__TAURI__.core.invoke(cmd, args);
      }
      if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.quantum) {
        return new Promise((resolve, reject) => {
          const callId = 'call_' + Math.random().toString(36).substring(2, 9);
          if (!window._nativeCallbacks) window._nativeCallbacks = {};
          window._nativeCallbacks[callId] = { resolve, reject };
          window.webkit.messageHandlers.quantum.postMessage({ id: callId, cmd: cmd, args: args });
        });
      }
      if (window.pywebview && window.pywebview.api && window.pywebview.api[cmd]) {
        return await window.pywebview.api[cmd](...Object.values(args));
      }
    } catch (err) {
      console.warn(`[Bridge Call Error ${cmd}]:`, err);
      throw err;
    }
    return null;
  }


  onBridgeReady() {
    console.log('[Quantum Updater]: Backend bridge connected.');
    this.loadInitialDiagnostics();
    this.loadInstalledSilent();

    // Auto scan on start after brief initial render
    setTimeout(() => {
      this.scanUpdates();
    }, 450);

    // Auto check for application self-update after initial scan launch
    setTimeout(() => {
      this.checkAutoUpdateOnStartup();
    }, 1500);
  }

  bindEvents() {
    // Brand link to quantumstep.in
    const brandLink = document.getElementById('brand-link');
    if (brandLink) {
      brandLink.addEventListener('click', () => {
        if (window.__TAURI__ && window.__TAURI__.opener) {
          window.__TAURI__.opener.openUrl('https://quantumstep.in').catch(() => {});
        } else {
          window.open('https://quantumstep.in', '_blank');
        }
      });
    }

    // Navigation Tabs
    document.querySelectorAll('.pill-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        const tab = btn.dataset.tab;
        this.switchTab(tab);
      });
    });

    // Theme Toggle
    const themeBtn = document.getElementById('theme-toggle-btn');
    if (themeBtn) {
      themeBtn.addEventListener('click', () => this.toggleTheme());
    }
  }

  toggleTheme() {
    const isLight = document.body.classList.toggle('light-mode');
    document.body.classList.toggle('dark-mode', !isLight);
    const theme = isLight ? 'light' : 'dark';
    this.invoke('save_setting', { key: 'theme', value: theme }).catch(() => {});
  }

  switchTab(tabId) {
    this.activeTab = tabId;
    document.querySelectorAll('.pill-btn').forEach(btn => {
      btn.classList.toggle('active', btn.dataset.tab === tabId);
    });
    document.querySelectorAll('.tab-pane').forEach(pane => {
      pane.classList.toggle('active', pane.id === `tab-${tabId}`);
    });

    if (tabId === 'explore') {
      this.renderExplore();
    } else if (tabId === 'installed' && this.installed.length === 0) {
      this.loadInstalled();
    } else if (tabId === 'cleanup' && (!this.cleanupData || !this.cleanupData.categories)) {
      this.scanCleanup();
    } else if (tabId === 'settings') {
      this.loadInitialDiagnostics();
    }
  }


  // ================= Universal Logo & Monogram Resolver =================

  getMonogram(name) {
    const clean = (name || '').trim().replace(/[^a-zA-Z0-9\s]/g, '');
    const words = clean.split(/\s+/).filter(Boolean);
    if (!words.length) return 'APP';
    if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
    return (words[0][0] + words[1][0]).toUpperCase();
  }

  getGradient(name) {
    let hash = 0;
    for (let i = 0; i < (name || '').length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    const idx = Math.abs(hash) % MONOGRAM_PALETTES.length;
    const [c1, c2] = MONOGRAM_PALETTES[idx];
    return `linear-gradient(135deg, ${c1}, ${c2})`;
  }

  /**
   * Resolves the authentic official software logo for any package across Updates, Explore, and Installed tabs.
   * Cascades through exact IDs, curated catalog, keyword dictionaries, and falls back cleanly to high-contrast monogram.
   */
  getAppLogoHtml(pkgId, pkgName) {
    const id = (pkgId || '').toLowerCase().trim();
    const name = (pkgName || '').toLowerCase().trim();
    const mono = this.getMonogram(pkgName || pkgId);
    const grad = this.getGradient(pkgName || pkgId);
    const safeName = this.escapeHtml(pkgName || pkgId);

    // 1. Direct Catalog Match
    const catItem = APP_CATALOG.find(c => c.id.toLowerCase() === id);
    if (catItem) {
      return `
        <div class="app-logo-container" style="background: ${catItem.bg || 'rgba(255,255,255,0.06)'};" title="${safeName}">
          <img class="app-logo-img" src="${catItem.logo}" alt="${safeName}" loading="lazy"
               onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">
          <span class="app-logo-fallback" style="display: none; background: ${grad};">${mono}</span>
        </div>
      `;
    }

    // 2. Exact Map Match
    let match = APP_LOGOS_MAP[id];

    // 3. Keyword / Substring Match
    if (!match) {
      for (const [key, val] of Object.entries(APP_KEYWORD_LOGOS)) {
        if (id.includes(key) || name.includes(key)) {
          match = val;
          break;
        }
      }
    }

    if (match) {
      const logoUrl = match.url || `https://cdn.simpleicons.org/${match.slug}.svg`;
      const bg = match.bg || 'rgba(255,255,255,0.06)';
      return `
        <div class="app-logo-container" style="background: ${bg};" title="${safeName}">
          <img class="app-logo-img" src="${logoUrl}" alt="${safeName}" loading="lazy"
               onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">
          <span class="app-logo-fallback" style="display: none; background: ${grad};">${mono}</span>
        </div>
      `;
    }

    // 4. Clean Fallback to Monogram
    return `
      <div class="app-logo-container" style="background: rgba(255,255,255,0.05);" title="${safeName}">
        <span class="app-logo-fallback" style="background: ${grad};">${mono}</span>
      </div>
    `;
  }


  // ================= Sequential Operation Queue & Installed Detection =================

  async loadInstalledSilent() {
    try {
      const packages = await this.invoke('get_installed', { engine: this.activeEngine });
      if (packages && Array.isArray(packages)) {
        this.installed = packages;
        this.installedSet.clear();
        packages.forEach(p => {
          if (p.id) this.installedSet.add(p.id.toLowerCase().trim());
          if (p.name) this.installedSet.add(p.name.toLowerCase().trim());
        });
        this.renderExplore();
      }
    } catch (err) {
      console.warn('[Quantum Updater]: Silent installed audit notice:', err);
    }
  }

  isPackageInstalled(pkgId, pkgName) {
    if (!pkgId) return false;
    const id = pkgId.toLowerCase().trim();
    if (this.installedSet.has(id)) return true;
    if (pkgName && this.installedSet.has(pkgName.toLowerCase().trim())) return true;
    for (const instId of this.installedSet) {
      if (instId === id || instId.includes(id) || id.includes(instId)) return true;
    }
    return false;
  }

  enqueueTask(task) {
    task.id = Date.now() + Math.random().toString(36).substr(2, 5);
    task.status = 'pending';
    task.timestamp = new Date();
    this.operationQueue.push(task);
    this.showToast(`Queued ${task.name} (${task.type})`, 'info');
    this.updateGlobalDock();
    this.renderQueueDrawer();

    if (!this.isProcessingQueue) {
      this.processNextQueueItem();
    }
  }

  async processNextQueueItem() {
    const nextTask = this.operationQueue.find(t => t.status === 'pending');
    if (!nextTask) {
      this.isProcessingQueue = false;
      this.currentQueueTask = null;
      this.updateGlobalDock();
      return;
    }

    this.isProcessingQueue = true;
    this.currentQueueTask = nextTask;
    nextTask.status = 'running';
    this.updateGlobalDock();
    this.renderQueueDrawer();

    const typeLabels = { install: 'Installing', upgrade: 'Upgrading', uninstall: 'Uninstalling' };
    const actionLabel = typeLabels[nextTask.type] || 'Processing';
    this.appendLog(`\n[Queue Manager]: ${actionLabel} ${nextTask.name} (${nextTask.pkgId})...\n`);

    try {
      let success = false;
      if (nextTask.type === 'install') {
        const res = await this.invoke('install_package', { packageId: nextTask.pkgId, engine: this.activeEngine });
        success = res && res.success !== false;
      } else if (nextTask.type === 'upgrade') {
        const res = await this.invoke('upgrade_packages', { packageIds: [nextTask.pkgId], engine: this.activeEngine });
        success = res && res.length > 0 && res[0].success !== false;
      } else if (nextTask.type === 'uninstall') {
        const res = await this.invoke('uninstall_package', { packageId: nextTask.pkgId, engine: this.activeEngine });
        success = res === true || res === null;
      }

      if (success) {
        nextTask.status = 'success';
        if (nextTask.type === 'install' || nextTask.type === 'upgrade') {
          this.installedSet.add(nextTask.pkgId.toLowerCase().trim());
          if (nextTask.name) this.installedSet.add(nextTask.name.toLowerCase().trim());
        } else if (nextTask.type === 'uninstall') {
          this.installedSet.delete(nextTask.pkgId.toLowerCase().trim());
        }
        this.showToast(`Successfully completed: ${nextTask.name}`, 'success');
        this.appendLog(`[Queue Manager]: Successfully finished ${nextTask.name}.\n`);
      } else {
        nextTask.status = 'failed';
        this.showToast(`Notice: ${nextTask.name} finished with status note`, 'error');
        this.appendLog(`[Queue Manager]: Notice on ${nextTask.name}.\n`);
      }
    } catch (err) {
      nextTask.status = 'failed';
      nextTask.error = String(err);
      this.showToast(`Failed: ${nextTask.name} - ${err}`, 'error');
      this.appendLog(`[Queue Manager Error]: ${err}\n`);
    }

    this.renderExplore();
    this.updateGlobalDock();
    this.renderQueueDrawer();

    setTimeout(() => this.processNextQueueItem(), 400);
  }

  updateGlobalDock() {
    const dock = document.getElementById('global-operation-dock');
    if (!dock) return;

    const pendingOrRunning = this.operationQueue.filter(t => t.status === 'pending' || t.status === 'running');
    const countBadge = document.getElementById('dock-queue-count');
    if (countBadge) countBadge.textContent = pendingOrRunning.length;

    if (this.currentQueueTask && this.isProcessingQueue) {
      dock.style.display = 'flex';
      const dot = document.getElementById('dock-pulse-dot');
      if (dot) { dot.className = 'dock-pulse-dot'; }
      const title = document.getElementById('dock-status-title');
      if (title) title.textContent = `${this.currentQueueTask.type.toUpperCase()}: ${this.currentQueueTask.name}`;
      const ticker = document.getElementById('dock-status-ticker');
      if (ticker) ticker.textContent = `Processing package ${this.currentQueueTask.pkgId} via ${this.activeEngine.toUpperCase()}... (${pendingOrRunning.length} remaining)`;
    } else if (this.operationQueue.length > 0) {
      dock.style.display = 'flex';
      const dot = document.getElementById('dock-pulse-dot');
      if (dot) { dot.className = 'dock-pulse-dot success'; }
      const title = document.getElementById('dock-status-title');
      if (title) title.textContent = 'Queue Finished';
      const ticker = document.getElementById('dock-status-ticker');
      if (ticker) ticker.textContent = `All ${this.operationQueue.length} operations completed.`;
      setTimeout(() => {
        if (!this.isProcessingQueue && this.operationQueue.every(t => t.status !== 'pending' && t.status !== 'running')) {
          dock.style.display = 'none';
        }
      }, 5000);
    } else {
      dock.style.display = 'none';
    }
  }

  toggleQueueDrawer(forceState = null) {
    const drawer = document.getElementById('queue-drawer');
    if (!drawer) return;
    if (forceState !== null) {
      drawer.classList.toggle('active', forceState);
    } else {
      drawer.classList.toggle('active');
    }
    this.renderQueueDrawer();
  }

  renderQueueDrawer() {
    const list = document.getElementById('queue-items-list');
    const badge = document.getElementById('queue-drawer-badge');
    if (!list) return;

    if (badge) badge.textContent = `${this.operationQueue.length} total`;

    if (this.operationQueue.length === 0) {
      list.innerHTML = '<div class="queue-empty-state">No background operations currently queued.</div>';
      return;
    }

    let html = '';
    [...this.operationQueue].reverse().forEach(task => {
      const isRunning = task.status === 'running';
      html += `
        <div class="queue-item-row ${isRunning ? 'running' : ''}">
          <div class="queue-item-left">
            <span class="queue-item-name" title="${this.escapeHtml(task.pkgId)}">${this.escapeHtml(task.name)}</span>
          </div>
          <span class="queue-item-status-pill ${task.status}">${task.status}</span>
        </div>
      `;
    });
    list.innerHTML = html;
  }

  clearFinishedQueue() {
    this.operationQueue = this.operationQueue.filter(t => t.status === 'pending' || t.status === 'running');
    this.renderQueueDrawer();
    this.updateGlobalDock();
  }

  showToast(message, type = 'info') {
    const container = document.getElementById('global-toast-container');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `toast-glass toast-${type}`;
    
    const iconSvg = type === 'success' 
      ? '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#34d399" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg>'
      : type === 'error'
      ? '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#fb7185" stroke-width="2.5"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg>'
      : '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#818cf8" stroke-width="2.5"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>';

    toast.innerHTML = `
      <div class="toast-icon-box">${iconSvg}</div>
      <span>${this.escapeHtml(message)}</span>
    `;
    container.appendChild(toast);

    setTimeout(() => {
      toast.style.opacity = '0';
      toast.style.transform = 'translateX(40px)';
      toast.style.transition = 'all 0.3s ease';
      setTimeout(() => toast.remove(), 300);
    }, 4000);
  }

  // ================= Updates Logic =================

  async scanUpdates() {
    if (this.isScanning || this.isUpdating) return;
    this.isScanning = true;

    const btnScan = document.getElementById('btn-scan');
    btnScan.disabled = true;
    btnScan.innerHTML = `<svg class="spin" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><path d="M3 3v5h5"></path><path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16"></path><path d="M16 21h5v-5"></path></svg> Scanning System...`;

    this.setIslandState(true, 'Scanning System for Application Updates...', null, `Auditing packages via ${this.activeEngine.toUpperCase()}...`);
    this.appendLog(`[Quantum Updater]: Initiating system audit using ${this.activeEngine.toUpperCase()}...\n`);

    try {
      const result = await this.invoke('scan_updates', { engine: this.activeEngine });
      if (result) {
        this.updates = result;
      } else {
        this.updates = [];
      }
      this.selectedIds.clear();
      this.renderUpdates();
      this.setIslandState(false, 'System Audit Complete', 100, `Found ${this.updates.length} applications with available updates.`);
    } catch (e) {
      console.error(e);
      this.appendLog(`[ERROR]: Scan failed: ${e}\n`);
      this.setIslandState(false, 'Scan Notice', 0, String(e));
    } finally {
      this.isScanning = false;
      btnScan.disabled = false;
      btnScan.innerHTML = `
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><path d="M3 3v5h5"></path><path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16"></path><path d="M16 21h5v-5"></path></svg>
        <span>Scan for Updates</span>
      `;
    }
  }

  renderUpdates(filteredList = null) {
    const list = filteredList !== null ? filteredList : this.updates;
    const container = document.getElementById('updates-list');

    // Update Bento metrics
    document.getElementById('bento-updates-val').textContent = this.updates.length;
    document.getElementById('bento-updates-sub').textContent = this.updates.length > 0 ? `${this.updates.length} updates pending` : 'All packages up-to-date';
    document.getElementById('bento-selected-val').textContent = this.selectedIds.size;
    document.getElementById('bento-selected-sub').textContent = this.selectedIds.size > 0 ? `${this.selectedIds.size} ready to install` : 'None ticked (All selected)';

    const navBadge = document.getElementById('nav-update-count');
    if (this.updates.length > 0) {
      navBadge.style.display = 'inline-block';
      navBadge.textContent = this.updates.length;
    } else {
      navBadge.style.display = 'none';
    }

    this.updateActionButtons();

    if (!list.length) {
      container.innerHTML = `
        <div class="glass-placeholder">
          <div class="placeholder-icon"><svg width="38" height="38" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg></div>
          <h3>System Up-to-Date</h3>
          <p>Great news! All installed applications monitored by this engine are currently on the latest version.</p>
        </div>
      `;
      return;
    }

    let html = '';
    list.forEach(pkg => {
      const isChecked = this.selectedIds.has(pkg.id);
      const logoHtml = this.getAppLogoHtml(pkg.id, pkg.name);

      html += `
        <div class="package-card ${isChecked ? 'selected' : ''}" id="card-${pkg.id}" onclick="app.handleCardClick('${pkg.id}', event)">
          <div class="package-left">
            <label class="custom-checkbox-container" onclick="event.stopPropagation()">
              <input type="checkbox" id="chk-${pkg.id}" ${isChecked ? 'checked' : ''} onchange="app.togglePackageSelect('${pkg.id}', this.checked)">
              <span class="checkmark"></span>
            </label>
            ${logoHtml}
            <div class="package-info">
              <span class="package-name">${this.escapeHtml(pkg.name)}</span>
              <div class="package-meta" id="meta-${pkg.id}">
                <span>${this.escapeHtml(pkg.id)}</span>
                <span class="version-pill">v${this.escapeHtml(pkg.installed_version || '?')}</span>
                <span><svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="5" y1="12" x2="19" y2="12"></line><polyline points="12 5 19 12 12 19"></polyline></svg></span>
                <span class="version-pill version-upgrade">v${this.escapeHtml(pkg.available_version || 'Latest')}</span>
              </div>
            </div>
          </div>
          <div class="package-right" onclick="event.stopPropagation()">
            <button class="glass-btn btn-primary btn-sm" id="btn-update-single-${pkg.id}" onclick="app.updateSinglePackage('${pkg.id}')">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"></polygon></svg> Upgrade
            </button>
          </div>
        </div>
      `;
    });

    container.innerHTML = html;
  }

  handleCardClick(id, event) {
    if (this.isUpdating) return;
    if (event.target.closest('button') || event.target.closest('input') || event.target.closest('.custom-checkbox-container')) {
      return;
    }
    const isChecked = !this.selectedIds.has(id);
    const chk = document.getElementById(`chk-${id}`);
    if (chk) chk.checked = isChecked;
    this.togglePackageSelect(id, isChecked);
  }

  togglePackageSelect(id, checked) {
    if (checked) {
      this.selectedIds.add(id);
    } else {
      this.selectedIds.delete(id);
    }
    const card = document.getElementById(`card-${id}`);
    if (card) card.classList.toggle('selected', checked);

    // Sync select-all checkbox
    const allBox = document.getElementById('check-select-all');
    if (allBox) allBox.checked = this.selectedIds.size === this.updates.length && this.updates.length > 0;

    // Sync Bento and Buttons live
    this.updateBentoCountersLive();
  }

  toggleSelectAll(checked) {
    if (checked) {
      this.updates.forEach(p => this.selectedIds.add(p.id));
    } else {
      this.selectedIds.clear();
    }
    this.renderUpdates();
  }

  updateBentoCountersLive() {
    const updatesVal = document.getElementById('bento-updates-val');
    const updatesSub = document.getElementById('bento-updates-sub');
    const selVal = document.getElementById('bento-selected-val');
    const selSub = document.getElementById('bento-selected-sub');
    const navBadge = document.getElementById('nav-update-count');

    if (updatesVal) updatesVal.textContent = this.updates.length;
    if (updatesSub) {
      updatesSub.textContent = this.updates.length > 0
        ? `${this.updates.length} updates pending`
        : 'All packages up-to-date';
    }

    if (selVal) selVal.textContent = this.selectedIds.size;
    if (selSub) {
      selSub.textContent = this.selectedIds.size > 0
        ? `${this.selectedIds.size} ready to install`
        : 'None ticked (All selected)';
    }

    if (navBadge) {
      if (this.updates.length > 0) {
        navBadge.style.display = 'inline-block';
        navBadge.textContent = this.updates.length;
      } else {
        navBadge.style.display = 'none';
      }
    }

    this.updateActionButtons();
  }

  updateActionButtons() {
    const btnAction = document.getElementById('btn-update-action');
    const btnText = document.getElementById('btn-update-text');

    if (!btnAction || !btnText) return;

    if (!this.updates.length || this.isUpdating) {
      btnAction.disabled = true;
      btnText.textContent = this.isUpdating ? 'Updating in Progress...' : 'Update All Packages';
      return;
    }

    btnAction.disabled = false;
    if (this.selectedIds.size === 0) {
      btnText.textContent = `Update All Packages (${this.updates.length})`;
    } else {
      btnText.textContent = `Update Selected (${this.selectedIds.size})`;
    }
  }

  filterUpdates(query) {
    const q = (query || '').toLowerCase().trim();
    if (!q) {
      this.renderUpdates();
      return;
    }
    const filtered = this.updates.filter(p =>
      (p.name || '').toLowerCase().includes(q) ||
      (p.id || '').toLowerCase().includes(q)
    );
    this.renderUpdates(filtered);
  }

  // ================= Multi / Single Upgrade Execution =================

  async startUpdateAction() {
    if (this.isUpdating) return;
    const targetIds = this.selectedIds.size > 0
      ? Array.from(this.selectedIds)
      : this.updates.map(p => p.id);

    if (!targetIds.length) return;
    await this.runBatchUpgrade(targetIds);
  }

  async updateSinglePackage(pkgId) {
    if (this.isUpdating) return;
    await this.runBatchUpgrade([pkgId]);
  }

  async runBatchUpgrade(packageIds) {
    if (this.isUpdating || !packageIds.length) return;
    this.isUpdating = true;
    this.updateActionButtons();

    const total = packageIds.length;
    this.appendLog(`\n======================================================\n`);
    this.appendLog(`[Quantum Updater]: Starting live update for ${total} package(s)...\n`);

    this.setIslandState(true, `Upgrading 1 of ${total}`, 0, `Initializing update engine...`);

    let completed = 0;
    let failed = 0;

    for (let i = 0; i < packageIds.length; i++) {
      const pid = packageIds[i];
      const pkgObj = this.updates.find(p => p.id === pid);
      const pkgDisplayName = pkgObj ? pkgObj.name : pid;
      const stepPct = Math.round(((i + 0.1) / total) * 100);

      this.setIslandState(true, `Upgrading ${i + 1} of ${total}: ${pkgDisplayName}`, stepPct, `Running silent update for ${pkgDisplayName}...`);
      this.appendLog(`\n[${i + 1}/${total}] Updating ${pkgDisplayName} (${pid})...\n`);

      // Set card to live updating state in-place
      const card = document.getElementById(`card-${pid}`);
      const singleBtn = document.getElementById(`btn-update-single-${pid}`);

      if (card) {
        card.classList.add('card-updating');
        let prog = card.querySelector('.card-inline-progress');
        if (!prog) {
          prog = document.createElement('div');
          prog.className = 'card-inline-progress';
          prog.innerHTML = '<div class="card-inline-progress-fill indeterminate"></div>';
          card.appendChild(prog);
        } else {
          const fill = prog.querySelector('.card-inline-progress-fill');
          if (fill) fill.className = 'card-inline-progress-fill indeterminate';
        }
      }

      if (singleBtn) {
        singleBtn.disabled = true;
        singleBtn.className = 'glass-btn btn-updating btn-sm';
        singleBtn.innerHTML = `<span class="btn-spinner"></span> <span>Updating...</span>`;
      }

      try {
        const statuses = await this.invoke('upgrade_packages', {
          packageIds: [pid],
          engine: this.activeEngine
        });

        const status = statuses && statuses[0];
        if (status && status.success) {
          completed++;

          // Visual state: Success on card
          if (card) {
            card.classList.remove('card-updating');
            card.classList.add('card-completed');
            const fill = card.querySelector('.card-inline-progress-fill');
            if (fill) fill.className = 'card-inline-progress-fill success';

            // Update version meta in-place
            const meta = document.getElementById(`meta-${pid}`);
            if (meta && pkgObj) {
              meta.innerHTML = `
                <span>${this.escapeHtml(pkgObj.id)}</span>
                <span class="version-pill version-upgrade">Up to date (v${this.escapeHtml(pkgObj.available_version || 'Latest')})</span>
              `;
            }
          }

          if (singleBtn) {
            singleBtn.className = 'glass-btn btn-completed btn-sm';
            singleBtn.innerHTML = `
              <svg class="check-icon" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414 0z" clip-rule="evenodd"/>
              </svg>
              <span>Upgraded</span>
            `;
          }

          this.appendLog(`[OK] Successfully upgraded ${pkgDisplayName} (${pid})\n`);

          // Live state decrement WITHOUT resetting or reloading the UI
          this.updates = this.updates.filter(p => p.id !== pid);
          this.selectedIds.delete(pid);
          this.updateBentoCountersLive();

          // Graceful celebration delay, then smoothly dismiss the card
          setTimeout(() => {
            if (card) {
              card.classList.add('card-dismiss');
              setTimeout(() => {
                if (card && card.parentNode) card.parentNode.removeChild(card);
                if (this.updates.length === 0) {
                  this.renderUpdates(); // Renders the clean "System Up-to-Date" placeholder
                }
              }, 450);
            }
          }, 2200);

        } else {
          failed++;
          if (card) {
            card.classList.remove('card-updating');
            const fill = card.querySelector('.card-inline-progress-fill');
            if (fill) fill.className = 'card-inline-progress-fill notice';
          }
          if (singleBtn) {
            singleBtn.disabled = false;
            singleBtn.className = 'glass-btn btn-secondary btn-sm';
            singleBtn.innerHTML = `Retry`;
            singleBtn.onclick = () => this.updateSinglePackage(pid);
          }
          this.appendLog(`[NOTICE] Upgrade finished with note: ${(status && status.message) || 'Check log'}\n`);
        }
      } catch (e) {
        failed++;
        if (card) {
          card.classList.remove('card-updating');
          const fill = card.querySelector('.card-inline-progress-fill');
          if (fill) fill.className = 'card-inline-progress-fill notice';
        }
        if (singleBtn) {
          singleBtn.disabled = false;
          singleBtn.className = 'glass-btn btn-secondary btn-sm';
          singleBtn.innerHTML = `Retry`;
          singleBtn.onclick = () => this.updateSinglePackage(pid);
        }
        this.appendLog(`[ERROR] Failed ${pid}: ${e}\n`);
      }
    }

    this.isUpdating = false;
    this.updateActionButtons();

    if (failed === 0) {
      this.setIslandState(false, 'All Upgrades Completed', 100, `Successfully upgraded ${completed} package(s).`);
    } else {
      this.setIslandState(false, 'Batch Completed with Notices', 100, `Upgraded: ${completed} | Notices: ${failed}`);
    }

    this.appendLog(`\n[Quantum Updater]: Batch operation finished. Success: ${completed}, Notices/Failures: ${failed}\n`);
    this.appendLog(`======================================================\n\n`);
  }

  // ================= Dynamic Island Controller =================

  setIslandState(active, title, pct, tickerText) {
    const titleEl = document.getElementById('island-status-title') || document.getElementById('island-task-title');
    const tickerEl = document.getElementById('island-status-ticker');
    const fillEl = document.getElementById('island-progress-fill');
    const badgeEl = document.getElementById('island-pct-badge');
    const pulseEl = document.getElementById('island-pulse');

    if (titleEl) titleEl.textContent = title;
    if (tickerEl) tickerEl.textContent = tickerText || '';
    if (pulseEl) pulseEl.style.display = active ? 'block' : 'none';

    if (pct !== null && pct !== undefined) {
      const p = Math.round(pct);
      fillEl.style.width = `${p}%`;
      badgeEl.textContent = `${p}%`;
    } else {
      fillEl.style.width = active ? '30%' : '0%';
      badgeEl.textContent = active ? 'Working...' : '';
    }
  }

  setIslandProgress(pctVal, tickerText) {
    const p = Math.round((pctVal || 0) * 100);
    const islandFill = document.getElementById('island-progress-fill');
    if (islandFill) islandFill.style.width = `${p}%`;
    const islandPct = document.getElementById('island-pct-badge');
    if (islandPct) islandPct.textContent = `${p}%`;
    if (tickerText) {
      const islandTicker = document.getElementById('island-status-ticker');
      if (islandTicker) islandTicker.textContent = tickerText;
    }

    // Mirror to App Update Progress Bar if active
    const updateProgFill = document.getElementById('update-prog-fill');
    if (updateProgFill) {
      updateProgFill.style.width = `${p}%`;
      const updateProgPct = document.getElementById('update-prog-pct');
      if (updateProgPct) updateProgPct.textContent = `${p}%`;
      const updateProgTicker = document.getElementById('update-prog-ticker');
      if (updateProgTicker && tickerText) updateProgTicker.textContent = tickerText;
    }
  }

  // ================= Explore Catalog (Automatic Loading, Filters & Sorting) =================

  setExploreCategory(category) {
    this.exploreCategory = category;
    document.querySelectorAll('.cat-pill').forEach(pill => {
      pill.classList.toggle('active', pill.dataset.category === category);
    });
    this.renderExplore();
  }

  setExploreSort(sortValue) {
    this.exploreSort = sortValue;
    this.renderExplore();
  }

  onExploreSearchInput(val) {
    this.exploreSearchTerm = (val || '').toLowerCase().trim();
    this.renderExplore();
  }

  renderExplore() {
    const container = document.getElementById('explore-results-list');
    if (!container) return;

    let items = [...APP_CATALOG];

    // 1. Filter by category
    if (this.exploreCategory === 'popular') {
      items = items.filter(it => (it.downloads || 0) >= 93);
    } else if (this.exploreCategory !== 'all') {
      items = items.filter(it => it.category === this.exploreCategory);
    }

    // 2. Filter by search term
    if (this.exploreSearchTerm) {
      const q = this.exploreSearchTerm;
      items = items.filter(it =>
        it.name.toLowerCase().includes(q) ||
        it.id.toLowerCase().includes(q) ||
        (it.desc || '').toLowerCase().includes(q) ||
        (it.category || '').toLowerCase().includes(q)
      );
    }

    // 3. Sort
    if (this.exploreSort === 'popular') {
      items.sort((a, b) => (b.downloads || 0) - (a.downloads || 0));
    } else if (this.exploreSort === 'name-asc') {
      items.sort((a, b) => a.name.localeCompare(b.name));
    } else if (this.exploreSort === 'name-desc') {
      items.sort((a, b) => b.name.localeCompare(a.name));
    } else if (this.exploreSort === 'category') {
      items.sort((a, b) => (a.category || '').localeCompare(b.category || '') || a.name.localeCompare(b.name));
    }

    // Update count badge
    const badge = document.getElementById('explore-count-badge');
    if (badge) {
      badge.textContent = `${items.length} Packages Available`;
    }

    if (!items.length) {
      container.innerHTML = `
        <div class="glass-placeholder">
          <div class="placeholder-icon"><svg width="38" height="38" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg></div>
          <h3>No Matching Applications</h3>
          <p>No curated software matched "${this.escapeHtml(this.exploreSearchTerm)}". Click "Search Online" to query the live repository.</p>
        </div>
      `;
      return;
    }

    const catLabels = {
      all: 'All Categories',
      popular: 'Most Popular',
      ai: 'AI & Machine Learning',
      development: 'Development',
      browsers: 'Web Browsers',
      productivity: 'Productivity',
      media: 'Music & Media',
      utilities: 'Addons & Utilities',
      gaming: 'Gaming & Social'
    };

    let html = '';
    items.forEach(pkg => {
      const logoHtml = this.getAppLogoHtml(pkg.id, pkg.name);
      const isInstalled = this.isPackageInstalled(pkg.id, pkg.name);
      const catLabel = catLabels[pkg.category] || 'Software';

      html += `
        <div class="package-card ${isInstalled ? 'card-installed' : ''}" id="explore-card-${pkg.id}">
          <div class="package-left">
            ${logoHtml}
            <div class="package-info">
              <div style="display: flex; align-items: center; gap: 8px; flex-wrap: wrap;">
                <span class="package-name">${this.escapeHtml(pkg.name)}</span>
                <span class="badge-cat">${catLabel}</span>
                ${isInstalled ? `<span class="badge-installed-tag"><svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg> Installed</span>` : ''}
              </div>
              <p class="package-card-desc">${this.escapeHtml(pkg.desc || '')}</p>
              <div class="package-meta">
                <span>${this.escapeHtml(pkg.id)}</span>
                <span class="version-pill">v${this.escapeHtml(pkg.version || 'Latest')}</span>
                <span>Score: ${pkg.downloads || 90}%</span>
              </div>
            </div>
          </div>
          <div class="package-right" style="display: flex; flex-direction: column; align-items: flex-end; gap: 6px;">
            ${isInstalled ? `
              <button class="glass-btn btn-installed-disabled btn-sm" disabled title="Already installed on this system">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg> Installed
              </button>
              <button class="glass-btn btn-secondary btn-xs" style="opacity: 0.75;" onclick="app.installExplorePackage('${pkg.id}', this, true)">Reinstall</button>
            ` : `
              <button class="glass-btn btn-coral btn-sm" id="btn-explore-inst-${pkg.id}" onclick="app.installExplorePackage('${pkg.id}', this)">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="12" y1="5" x2="12" y2="19"></line><line x1="5" y1="12" x2="19" y2="12"></line></svg> Install
              </button>
            `}
          </div>
        </div>
      `;
    });

    container.innerHTML = html;
  }

  async searchRepositoryCatalog() {
    const input = document.getElementById('input-explore-search');
    const query = (input ? input.value : '').trim();
    if (!query) {
      this.renderExplore();
      return;
    }

    const container = document.getElementById('explore-results-list');
    container.innerHTML = `
      <div class="glass-placeholder">
        <div class="placeholder-icon"><svg width="38" height="38" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg></div>
        <h3>Searching Official Repository...</h3>
        <p>Querying ${this.activeEngine.toUpperCase()} catalog for "${this.escapeHtml(query)}"</p>
      </div>
    `;

    this.appendLog(`[Quantum Updater]: Searching repository for: "${query}"...\n`);

    try {
      const results = await this.invoke('search_catalog', {
        query: query,
        engine: this.activeEngine
      });

      if (!results || !results.length) {
        container.innerHTML = `
          <div class="glass-placeholder">
            <div class="placeholder-icon"><svg width="38" height="38" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg></div>
            <h3>No Online Packages Found</h3>
            <p>No repository packages matched "${this.escapeHtml(query)}". Try another search keyword.</p>
          </div>
        `;
        return;
      }

      let html = `
        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; padding: 0 4px;">
          <span style="font-size: 13px; color: var(--text-muted); font-weight: 600;">Live Search Results (${results.length})</span>
          <button class="glass-btn btn-secondary btn-sm" onclick="app.renderExplore()">← Back to Curated Explore</button>
        </div>
      `;

      results.forEach(item => {
        const logoHtml = this.getAppLogoHtml(item.id, item.name);
        html += `
          <div class="package-card">
            <div class="package-left">
              ${logoHtml}
              <div class="package-info">
                <span class="package-name">${this.escapeHtml(item.name)}</span>
                <div class="package-meta">
                  <span>${this.escapeHtml(item.id)}</span>
                  <span class="version-pill">v${this.escapeHtml(item.version || 'Latest')}</span>
                  <span>(${this.escapeHtml(item.source || 'winget')})</span>
                </div>
              </div>
            </div>
            <div class="package-right">
              <button class="glass-btn btn-primary btn-sm" onclick="app.installExplorePackage('${item.id}', this)">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="12" y1="5" x2="12" y2="19"></line><line x1="5" y1="12" x2="19" y2="12"></line></svg> Install
              </button>
            </div>
          </div>
        `;
      });

      container.innerHTML = html;
    } catch (e) {
      container.innerHTML = `
        <div class="glass-placeholder">
          <div class="placeholder-icon"><svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg></div>
          <h3>Search Notice</h3>
          <p>${this.escapeHtml(String(e))}</p>
        </div>
      `;
    }
  }

  async installExplorePackage(pkgId, btnEl) {
    // Find card and package metadata
    const card = document.getElementById(`explore-card-${pkgId}`) || (btnEl ? btnEl.closest('.package-card') : null);
    const catItem = APP_CATALOG.find(p => p.id.toLowerCase() === pkgId.toLowerCase());
    const pkgDisplayName = catItem ? catItem.name : pkgId;

    if (card) {
      card.classList.add('card-updating');
      let prog = card.querySelector('.card-inline-progress');
      if (!prog) {
        prog = document.createElement('div');
        prog.className = 'card-inline-progress';
        prog.innerHTML = '<div class="card-inline-progress-fill indeterminate"></div>';
        card.appendChild(prog);
      } else {
        const fill = prog.querySelector('.card-inline-progress-fill');
        if (fill) fill.className = 'card-inline-progress-fill indeterminate';
      }
    }

    if (btnEl) {
      btnEl.disabled = true;
      btnEl.className = 'glass-btn btn-updating btn-sm';
      btnEl.innerHTML = `<span class="btn-spinner"></span> <span>Installing...</span>`;
    }

    this.setIslandState(true, `Installing ${pkgDisplayName}`, null, `Downloading & running silent setup via ${this.activeEngine.toUpperCase()}...`);
    this.appendLog(`[Quantum Updater]: Initiating silent install for "${pkgDisplayName}" (${pkgId})...\n`);

    try {
      const res = await this.invoke('install_package', {
        packageId: pkgId,
        engine: this.activeEngine
      });

      if (res && res.success) {
        if (card) {
          card.classList.remove('card-updating');
          card.classList.add('card-completed');
          const fill = card.querySelector('.card-inline-progress-fill');
          if (fill) fill.className = 'card-inline-progress-fill success';
        }

        if (btnEl) {
          btnEl.className = 'glass-btn btn-completed btn-sm';
          btnEl.innerHTML = `
            <svg class="check-icon" viewBox="0 0 20 20" fill="currentColor">
              <path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414 0z" clip-rule="evenodd"/>
            </svg>
            <span>Installed</span>
          `;
        }

        this.installedSet.add(pkgId.toLowerCase());
        this.setIslandState(false, `${pkgDisplayName} Installed`, 100, `${pkgDisplayName} is installed and ready to use.`);
        this.appendLog(`[OK] Successfully installed "${pkgDisplayName}" (${pkgId}).\n`);

        // Smoothly clean up celebration pulse after delay without resetting UI
        setTimeout(() => {
          if (card) {
            card.classList.remove('card-completed');
            const prog = card.querySelector('.card-inline-progress');
            if (prog) prog.remove();
          }
        }, 3200);

      } else {
        if (card) {
          card.classList.remove('card-updating');
          const fill = card.querySelector('.card-inline-progress-fill');
          if (fill) fill.className = 'card-inline-progress-fill notice';
        }
        if (btnEl) {
          btnEl.disabled = false;
          btnEl.className = 'glass-btn btn-secondary btn-sm';
          btnEl.innerHTML = `Retry Install`;
          btnEl.onclick = () => this.installExplorePackage(pkgId, btnEl);
        }
        this.setIslandState(false, `Install Notice`, null, `${pkgDisplayName} notice: ${(res && res.message) || 'Check console'}`);
      }
    } catch (e) {
      if (card) {
        card.classList.remove('card-updating');
        const fill = card.querySelector('.card-inline-progress-fill');
        if (fill) fill.className = 'card-inline-progress-fill notice';
      }
      if (btnEl) {
        btnEl.disabled = false;
        btnEl.className = 'glass-btn btn-secondary btn-sm';
        btnEl.innerHTML = `Retry Install`;
        btnEl.onclick = () => this.installExplorePackage(pkgId, btnEl);
      }
      this.setIslandState(false, `Install Error`, null, String(e));
      this.appendLog(`[ERROR]: Installation failed for "${pkgDisplayName}": ${e}\n`);
    }
  }

  // ================= Installed Applications Tab =================

  async loadInstalled() {
    const container = document.getElementById('installed-list');
    container.innerHTML = `
      <div class="glass-placeholder">
        <div class="placeholder-icon"><svg class="spin" width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><path d="M3 3v5h5"></path><path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16"></path><path d="M16 21h5v-5"></path></svg></div>
        <h3>Auditing System Applications...</h3>
        <p>Reading registry and package manager database...</p>
      </div>
    `;

    try {
      const packages = await this.invoke('get_installed', { engine: this.activeEngine });
      this.installed = packages || [];
      this.installedSet.clear();
      this.installed.forEach(p => this.installedSet.add(p.id.toLowerCase()));
      this.renderInstalled();
    } catch (e) {
      container.innerHTML = `<div class="glass-placeholder"><p>Failed to load installed apps: ${e}</p></div>`;
    }
  }

  renderInstalled(filteredList = null) {
    const list = filteredList !== null ? filteredList : this.installed;
    const container = document.getElementById('installed-list');

    if (!list.length) {
      container.innerHTML = `<div class="glass-placeholder"><h3>No applications found</h3></div>`;
      return;
    }

    let html = '';
    list.forEach(pkg => {
      const logoHtml = this.getAppLogoHtml(pkg.id, pkg.name);

      html += `
        <div class="package-card">
          <div class="package-left">
            ${logoHtml}
            <div class="package-info">
              <span class="package-name">${this.escapeHtml(pkg.name)}</span>
              <div class="package-meta">
                <span>${this.escapeHtml(pkg.id)}</span>
                <span class="version-pill">v${this.escapeHtml(pkg.installed_version || '?')}</span>
              </div>
            </div>
          </div>
          <div class="package-right">
            <button class="glass-btn btn-secondary btn-sm" onclick="app.uninstallPackage('${pkg.id}', '${this.escapeHtml(pkg.name)}')">
              Uninstall
            </button>
          </div>
        </div>
      `;
    });
    container.innerHTML = html;
  }

  filterInstalled(query) {
    const q = (query || '').toLowerCase().trim();
    if (!q) {
      this.renderInstalled();
      return;
    }
    const filtered = this.installed.filter(p =>
      (p.name || '').toLowerCase().includes(q) ||
      (p.id || '').toLowerCase().includes(q)
    );
    this.renderInstalled(filtered);
  }

  async uninstallPackage(pkgId, pkgName) {
    if (!confirm(`Are you sure you want to uninstall "${pkgName}" (${pkgId})?`)) return;
    this.appendLog(`[Quantum Updater]: Uninstalling "${pkgId}"...\n`);
    await this.invoke('uninstall_package', { packageId: pkgId, engine: this.activeEngine });
    this.loadInstalled();
  }

  // ================= Terminal & Console =================

  appendLog(line) {
    const output = document.getElementById('console-output');
    if (!output) return;
    const el = document.createElement('div');
    el.className = 'terminal-line';
    el.textContent = line;
    output.appendChild(el);
    output.scrollTop = output.scrollHeight;
  }

  clearConsoleLog() {
    const output = document.getElementById('console-output');
    if (output) output.innerHTML = '<div class="terminal-line system">[Quantum Updater]: Log cleared.</div>';
  }

  copyConsoleLog() {
    const output = document.getElementById('console-output');
    if (output) {
      navigator.clipboard.writeText(output.innerText);
      alert('Console logs copied to clipboard!');
    }
  }

  // ================= Mac System Cleanup & Storage Reclaimer =================

  async scanCleanup() {

    const btn = document.getElementById('btn-scan-cleanup');
    if (btn) btn.classList.add('loading');
    this.appendLog('[Quantum Clean]: Scanning system storage, app caches, developer bloat, and orphaned data...');

    try {
      const res = await this.invoke('scan_cleanup');
      if (res) {
        this.cleanupData = res;
        this.selectedCleanupCategories = new Set(
          res.categories.filter(c => c.selected && c.size_bytes > 0).map(c => c.id)
        );
        this.renderCleanupMetrics();
        this.renderCleanupCategories();
        this.appendLog(`[Quantum Clean]: Scan complete. Identified ${res.total_reclaimable_str} across ${res.total_files.toLocaleString()} files.`);
      }
    } catch (err) {
      this.appendLog(`[Quantum Clean Error]: ${err.message || err}`);
      this.showToast('Failed to scan system storage', 'error');
    } finally {
      if (btn) btn.classList.remove('loading');
    }
  }

  renderCleanupMetrics() {
    if (!this.cleanupData) return;
    const d = this.cleanupData;

    // Total Reclaimable
    const totalEl = document.getElementById('cleanup-reclaimable-total');
    if (totalEl) totalEl.textContent = d.total_reclaimable_str;
    const subEl = document.getElementById('cleanup-reclaimable-sub');
    if (subEl) subEl.textContent = `Found ${d.total_files.toLocaleString()} cache & log files`;

    // Health
    const healthEl = document.getElementById('cleanup-health-status');
    if (healthEl) {
      if (d.total_reclaimable_bytes > 2 * 1024 * 1024 * 1024) {
        healthEl.textContent = 'Cleanup Recommended';
        healthEl.className = 'bento-value text-golden';
      } else if (d.total_reclaimable_bytes > 0) {
        healthEl.textContent = 'Ready to Optimize';
        healthEl.className = 'bento-value text-mint';
      } else {
        healthEl.textContent = 'Storage Optimized';
        healthEl.className = 'bento-value text-mint';
      }
    }

    // Disk storage
    const diskEl = document.getElementById('cleanup-disk-free');
    if (diskEl) diskEl.textContent = `${d.disk_free_str} Free`;
    const diskSub = document.getElementById('cleanup-disk-sub');
    if (diskSub) diskSub.textContent = `${d.disk_used_str} of ${d.disk_total_str} used (${d.disk_used_percent}%)`;

    this.updateCleanupSelectedMetric();
  }

  updateCleanupSelectedMetric() {
    if (!this.cleanupData || !this.cleanupData.categories) return;
    let selectedBytes = 0;
    let selectedCount = 0;

    this.cleanupData.categories.forEach(cat => {
      if (this.selectedCleanupCategories.has(cat.id)) {
        selectedBytes += cat.size_bytes;
        selectedCount++;
      }
    });

    const countEl = document.getElementById('cleanup-selected-count');
    if (countEl) countEl.textContent = `${selectedCount} Categories`;
    const bytesEl = document.getElementById('cleanup-selected-bytes');
    if (bytesEl) bytesEl.textContent = `${this.formatBytes(selectedBytes)} selected for reclamation`;

    // Update nav counter badge
    const badge = document.getElementById('nav-cleanup-badge');
    if (badge) {
      if (selectedCount > 0) {
        badge.textContent = selectedCount;
        badge.style.display = 'inline-block';
      } else {
        badge.style.display = 'none';
      }
    }
  }

  renderCleanupCategories() {
    const container = document.getElementById('cleanup-categories-container');
    if (!container || !this.cleanupData || !this.cleanupData.categories) return;

    if (this.cleanupData.categories.length === 0) {
      container.innerHTML = `
        <div class="glass-placeholder">
          <h3>No cleanup targets found</h3>
          <p>Your system caches are clean.</p>
        </div>
      `;
      return;
    }

    let html = '';
    this.cleanupData.categories.forEach(cat => {
      const isSelected = this.selectedCleanupCategories.has(cat.id);
      const isZero = cat.size_bytes === 0;
      const sizeClass = isZero ? 'size-zero' : (cat.size_bytes > 1024 * 1024 * 1024 ? 'size-large' : '');

      let subItemsHtml = '';
      if (cat.sub_items && cat.sub_items.length > 0) {
        subItemsHtml = `
          <div class="cleanup-cat-subitems">
            ${cat.sub_items.slice(0, 5).map(sub => `
              <div class="cleanup-subitem-row">
                <span class="cleanup-subitem-name" title="${this.escapeHtml(sub.name)}">${this.escapeHtml(sub.name)}</span>
                <span class="cleanup-subitem-size">${sub.size_str}</span>
              </div>
            `).join('')}
          </div>
        `;
      }

      html += `
        <div class="cleanup-category-card ${isSelected ? 'selected' : ''}" id="cat-card-${cat.id}">
          <div>
            <div class="cleanup-cat-top">
              <div class="cleanup-cat-icon-box">
                ${this.getCleanupIconSvg(cat.svg_icon)}
              </div>
              <div class="cleanup-cat-meta">
                <h4>${this.escapeHtml(cat.name)}</h4>
                <span class="cleanup-cat-tag">${this.escapeHtml(cat.tag)}</span>
              </div>
              <span class="cleanup-size-pill ${sizeClass}">${cat.size_str}</span>
            </div>
            <p class="cleanup-cat-desc">${this.escapeHtml(cat.description)}</p>
            ${subItemsHtml}
          </div>

          <div class="cleanup-cat-footer">
            <label class="cleanup-checkbox-label">
              <input type="checkbox" ${isSelected ? 'checked' : ''} ${isZero ? 'disabled' : ''} onchange="app.toggleCleanupCategory('${cat.id}', this.checked)">
              <span>${isSelected ? 'Selected' : 'Exclude'}</span>
            </label>
            <span style="font-size: 11px; color: var(--text-muted); font-family: var(--font-mono);">${cat.file_count.toLocaleString()} files</span>
          </div>
        </div>
      `;
    });

    container.innerHTML = html;
  }

  getCleanupIconSvg(type) {
    switch (type) {
      case 'database':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><ellipse cx="12" cy="5" rx="9" ry="3"></ellipse><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"></path><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"></path></svg>';
      case 'code':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="16 18 22 12 16 6"></polyline><polyline points="8 6 2 12 8 18"></polyline></svg>';
      case 'file-text':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline><line x1="16" y1="13" x2="8" y2="13"></line><line x1="16" y1="17" x2="8" y2="17"></line></svg>';
      case 'trash-2':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>';
      case 'archive':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="21 8 21 21 3 21 3 8"></polyline><rect x="1" y="3" width="22" height="5"></rect><line x1="10" y1="12" x2="14" y2="12"></line></svg>';
      case 'cpu':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="4" y="4" width="16" height="16" rx="2" ry="2"></rect><rect x="9" y="9" width="6" height="6"></rect><line x1="9" y1="1" x2="9" y2="4"></line><line x1="15" y1="1" x2="15" y2="4"></line><line x1="9" y1="20" x2="9" y2="23"></line><line x1="15" y1="20" x2="15" y2="23"></line><line x1="20" y1="9" x2="23" y2="9"></line><line x1="20" y1="14" x2="23" y2="14"></line><line x1="1" y1="9" x2="4" y2="9"></line><line x1="1" y1="14" x2="4" y2="14"></line></svg>';
      default:
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polygon points="12 2 2 7 12 12 22 7 12 2"></polygon><polyline points="2 17 12 22 22 17"></polyline><polyline points="2 12 12 17 22 12"></polyline></svg>';
    }
  }

  toggleCleanupCategory(catId, isChecked) {
    if (isChecked) {
      this.selectedCleanupCategories.add(catId);
    } else {
      this.selectedCleanupCategories.delete(catId);
    }
    const card = document.getElementById(`cat-card-${catId}`);
    if (card) card.classList.toggle('selected', isChecked);
    this.updateCleanupSelectedMetric();
  }

  toggleAllCleanup(onlySafe = false) {
    if (!this.cleanupData || !this.cleanupData.categories) return;
    const shouldSelectAll = this.selectedCleanupCategories.size < this.cleanupData.categories.filter(c => c.size_bytes > 0).length;

    this.selectedCleanupCategories.clear();
    if (shouldSelectAll) {
      this.cleanupData.categories.forEach(c => {
        if (c.size_bytes > 0 && (!onlySafe || c.risk === 'low')) {
          this.selectedCleanupCategories.add(c.id);
        }
      });
    }
    this.renderCleanupCategories();
    this.updateCleanupSelectedMetric();
  }

  async executeCleanup() {
    if (this.selectedCleanupCategories.size === 0) {
      this.showToast('Please select at least one category to purge', 'error');
      return;
    }

    const selectedList = Array.from(this.selectedCleanupCategories);
    let selectedBytes = 0;
    this.cleanupData.categories.forEach(c => {
      if (this.selectedCleanupCategories.has(c.id)) selectedBytes += c.size_bytes;
    });

    const confirmed = confirm(`Are you sure you want to purge ${this.formatBytes(selectedBytes)} across ${selectedList.length} categories?\n\nThis will safely delete temporary application caches and log files.`);
    if (!confirmed) return;

    this.isCleaning = true;
    const purgeBtn = document.getElementById('btn-purge-cleanup');
    if (purgeBtn) purgeBtn.classList.add('loading');

    this.setIslandProgress(0.1, `Starting Quantum Clean across ${selectedList.length} categories...`);
    this.appendLog(`[Quantum Clean]: Purging ${this.formatBytes(selectedBytes)} from categories: ${selectedList.join(', ')}`);

    try {
      await this.invoke('execute_cleanup', { categories: selectedList });
    } catch (err) {
      this.appendLog(`[Quantum Clean Error]: ${err.message || err}`);
      this.showToast('Cleanup failed to start', 'error');
      this.setIslandProgress(null, '');
      if (purgeBtn) purgeBtn.classList.remove('loading');
    }
  }

  onCleanupFinished(success, rawResult) {
    this.isCleaning = false;
    const purgeBtn = document.getElementById('btn-purge-cleanup');
    if (purgeBtn) purgeBtn.classList.remove('loading');
    this.setIslandProgress(null, '');

    let res = {};
    try {
      res = typeof rawResult === 'string' ? JSON.parse(rawResult) : rawResult;
    } catch (e) {
      res = { total_freed_str: 'storage' };
    }

    if (success) {
      const freed = res.total_freed_str || 'space';
      this.showToast(`Quantum Clean complete! Reclaimed ${freed}.`, 'success');
      this.appendLog(`[Quantum Clean]: Successfully finalized. Total space reclaimed: ${freed}.`);
      // Re-scan to update values
      setTimeout(() => this.scanCleanup(), 1000);
    } else {
      this.showToast(`Cleanup notice: ${res.error || 'Check console logs'}`, 'error');
    }
  }

  formatBytes(bytes) {
    if (!bytes || bytes === 0) return '0 B';
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
  }

  // ================= Settings & Diagnostics =================


  async loadInitialDiagnostics() {
    try {
      const diag = await this.invoke('get_diagnostics', { activeEngine: this.activeEngine });
      if (!diag) return;

      this.renderDiagnostics(diag);
      this.renderEngineSelector(diag.engines, diag.active_engine);

      // Admin badge
      const adminText = document.getElementById('admin-status-text');
      const adminPill = document.getElementById('admin-status-pill');
      if (diag.is_admin) {
        adminText.textContent = 'Elevated';
        adminPill.className = 'status-pill status-admin';
      } else {
        adminText.textContent = 'User Mode';
        adminPill.className = 'status-pill status-admin standard';
      }

      // Engine badge
      this.activeEngine = diag.active_engine || 'winget';
      document.getElementById('active-engine-badge').textContent = this.activeEngine.toUpperCase();
      document.getElementById('bento-engine-val').textContent = this.activeEngine.toUpperCase();
    } catch (e) {
      console.warn('Diagnostics notice:', e);
    }
  }

  renderDiagnostics(diag) {
    const container = document.getElementById('diagnostics-container');
    if (!container || !diag) return;

    const rows = [
      ['Publisher', 'Quantum Step (quantumstep.in)'],
      ['Privilege Level', diag.is_admin ? 'Administrator (Elevated)' : 'Standard User (Not Elevated)'],
      ['Operating System', diag.os_info || 'Windows'],
      ['CPU Architecture', diag.arch || 'x64'],
      ['Active Engine', diag.active_engine || 'winget'],
      ['Winget Version', diag.winget_version || 'Not Available'],
      ['Chocolatey Version', diag.choco_version || 'Not Available']
    ];

    let html = '';
    rows.forEach(([k, v]) => {
      html += `
        <div class="diag-row">
          <span class="diag-key">${k}</span>
          <span class="diag-val">${this.escapeHtml(v)}</span>
        </div>
      `;
    });
    container.innerHTML = html;
  }

  renderEngineSelector(engines, activeId) {
    const container = document.getElementById('engine-options-container');
    if (!container || !engines) return;

    let html = '';
    engines.forEach(eng => {
      const isSelected = eng.id === activeId;
      html += `
        <div class="engine-option-row ${isSelected ? 'selected' : ''}" onclick="app.selectEngine('${eng.id}')">
          <div>
            <strong>${eng.name}</strong>
            <span style="font-size: 12px; color: var(--text-muted); display: block;">${eng.available ? `v${eng.version} (Ready)` : 'Not installed'}</span>
          </div>
          <div>${isSelected ? '● Active' : ''}</div>
        </div>
      `;
    });
    container.innerHTML = html;
  }

  async selectEngine(engineId) {
    this.activeEngine = engineId;
    await this.invoke('set_engine', { engineId: engineId });
    this.loadInitialDiagnostics();
    this.scanUpdates();
  }

  savePreference(key, value) {
    this.invoke('save_setting', { key: key, value: value }).catch(() => {});
  }

  escapeHtml(str) {
    if (!str) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  // ================= In-App Self-Updater System =================

  async checkAutoUpdateOnStartup() {
    const prefToggle = document.getElementById('pref-auto-update-check');
    if (prefToggle && !prefToggle.checked) {
      return;
    }
    await this.checkForAppUpdates(true);
  }

  async checkForAppUpdates(silent = false) {
    if (this.isCheckingAppUpdate || this.isApplyingAppUpdate) return;
    this.isCheckingAppUpdate = true;

    const btnCheck = document.getElementById('btn-check-updates');
    if (btnCheck) {
      btnCheck.disabled = true;
      btnCheck.innerHTML = `
        <svg class="spin-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="12" cy="12" r="10"></circle>
          <path d="M12 2a10 10 0 0 1 10 10"></path>
        </svg>
        <span>Checking Feed...</span>
      `;
    }

    if (!silent) {
      this.appendLog('[Quantum Auto-Updater]: Connecting to GitHub release feed to verify version integrity...');
    }

    try {
      const res = await this.invoke('check_self_update');

      const timeEl = document.getElementById('update-last-checked-time');
      if (timeEl) {
        timeEl.textContent = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
      }

      if (res && res.has_update) {
        this.pendingUpdate = res;

        // Update Settings Status Badge
        const statusBadge = document.getElementById('app-update-status-badge');
        if (statusBadge) {
          statusBadge.className = 'update-status-badge pending';
          statusBadge.innerHTML = `<span class="status-dot"></span><span id="app-version-display">v${res.latest_version} • Update Available</span>`;
        }

        // Show update details
        const idleBox = document.getElementById('update-state-idle');
        const foundBox = document.getElementById('update-state-found');
        const progBox = document.getElementById('update-state-progress');
        if (idleBox) idleBox.style.display = 'none';
        if (progBox) progBox.style.display = 'none';
        if (foundBox) {
          foundBox.style.display = 'block';
          const tagEl = document.getElementById('update-new-ver-tag');
          if (tagEl) tagEl.textContent = `v${res.latest_version} Available`;
          const sizeEl = document.getElementById('update-new-ver-size');
          if (sizeEl) sizeEl.textContent = res.asset_size_str || '';
          const notesEl = document.getElementById('update-new-ver-notes');
          if (notesEl) notesEl.textContent = res.release_notes || res.release_name || 'Includes performance and security enhancements.';
        }

        // Show Install Action Button
        const btnInstall = document.getElementById('btn-install-app-update');
        if (btnInstall) btnInstall.style.display = 'inline-flex';

        // Show Global Header Banner
        const banner = document.getElementById('global-update-banner');
        if (banner) {
          const bannerText = document.getElementById('update-banner-text');
          if (bannerText) {
            bannerText.textContent = `Quantum Updater v${res.latest_version} is available. No setup redownload required.`;
          }
          banner.style.display = 'flex';
        }

        this.appendLog(`[Quantum Auto-Updater]: New release available: v${res.latest_version} (${res.asset_size_str || 'Verified'}).`);

      } else {
        this.pendingUpdate = null;

        // Set status badge to healthy
        const statusBadge = document.getElementById('app-update-status-badge');
        if (statusBadge) {
          statusBadge.className = 'update-status-badge';
          statusBadge.innerHTML = `<span class="status-dot healthy"></span><span id="app-version-display">v2.0.0 • Up to Date</span>`;
        }

        const idleBox = document.getElementById('update-state-idle');
        const foundBox = document.getElementById('update-state-found');
        const progBox = document.getElementById('update-state-progress');
        if (idleBox) idleBox.style.display = 'block';
        if (foundBox) foundBox.style.display = 'none';
        if (progBox) progBox.style.display = 'none';

        const btnInstall = document.getElementById('btn-install-app-update');
        if (btnInstall) btnInstall.style.display = 'none';

        if (!silent) {
          this.appendLog('[Quantum Auto-Updater]: Installed application is on the latest verified release.');
        }
      }

    } catch (err) {
      console.warn('[Auto-Updater Check Failed]:', err);
      if (!silent) {
        this.appendLog(`[Quantum Auto-Updater Warning]: Could not reach release feed: ${err.message || err}`);
      }
    } finally {
      this.isCheckingAppUpdate = false;
      if (btnCheck) {
        btnCheck.disabled = false;
        btnCheck.innerHTML = `
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="23 4 23 10 17 10"></polyline>
            <polyline points="1 20 1 14 7 14"></polyline>
            <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path>
          </svg>
          <span>Check for Updates</span>
        `;
      }
    }
  }

  async applySelfUpdate() {
    if (!this.pendingUpdate || !this.pendingUpdate.download_url) {
      this.appendLog('[Quantum Auto-Updater Error]: No active update package selected for installation.');
      return;
    }

    if (this.isApplyingAppUpdate) return;
    this.isApplyingAppUpdate = true;

    // Show Progress State in Settings Card
    const idleBox = document.getElementById('update-state-idle');
    const foundBox = document.getElementById('update-state-found');
    const progBox = document.getElementById('update-state-progress');
    if (idleBox) idleBox.style.display = 'none';
    if (foundBox) foundBox.style.display = 'none';
    if (progBox) progBox.style.display = 'block';

    const progTicker = document.getElementById('update-prog-ticker');
    if (progTicker) progTicker.textContent = `Downloading Quantum Updater v${this.pendingUpdate.latest_version}...`;
    const progFill = document.getElementById('update-prog-fill');
    if (progFill) progFill.style.width = '10%';

    // Update banner
    const btnBannerUpdate = document.getElementById('btn-banner-update');
    if (btnBannerUpdate) btnBannerUpdate.style.display = 'none';
    const bannerText = document.getElementById('update-banner-text');
    if (bannerText) bannerText.textContent = `Downloading update v${this.pendingUpdate.latest_version}... Relaunching automatically.`;

    const btnInstall = document.getElementById('btn-install-app-update');
    if (btnInstall) btnInstall.style.display = 'none';
    const btnCheck = document.getElementById('btn-check-updates');
    if (btnCheck) btnCheck.disabled = true;

    this.appendLog(`[Quantum Auto-Updater]: Initiating background update download from ${this.pendingUpdate.download_url}...`);

    try {
      await this.invoke('apply_self_update', { downloadUrl: this.pendingUpdate.download_url });
      if (progTicker) progTicker.textContent = 'Applying update & relaunching...';
      if (progFill) progFill.style.width = '100%';
    } catch (err) {
      console.error('[Apply Self-Update Failed]:', err);
      this.appendLog(`[Quantum Auto-Updater Error]: In-place update failed: ${err.message || err}`);
      this.isApplyingAppUpdate = false;
      if (btnCheck) btnCheck.disabled = false;
      if (btnInstall) btnInstall.style.display = 'inline-flex';
    }
  }

  dismissUpdateBanner() {
    const banner = document.getElementById('global-update-banner');
    if (banner) banner.style.display = 'none';
  }
}

// Native macOS WebKit asynchronous bridge response handler
window.onNativeResponse = function(callId, result, error) {
  if (window._nativeCallbacks && window._nativeCallbacks[callId]) {
    const cb = window._nativeCallbacks[callId];
    delete window._nativeCallbacks[callId];
    if (error) cb.reject(new Error(error));
    else cb.resolve(result);
  }
};

// Global instance
const app = new QuantumUpdaterApp();
window.app = app;

