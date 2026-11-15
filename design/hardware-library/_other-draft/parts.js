/* Quantum Updater hardware illustration library */
(function (root) {
  "use strict";

  var HARDWARE_ART = {
  "cpu.generic": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"13\" y=\"13\" width=\"30\" height=\"30\" rx=\"8\"/><path class=\"f\" d=\"M20 20h16v16H20z\" opacity=\"0.35\"/><path class=\"m\" d=\"M16 28h6M34 28h6M28 16v6M28 34v6\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"30\" font-size=\"7\">CPU</tspan></text></svg>",
  "cpu.intel.core.i3": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M20 6V14M20 42V50M6 20H14M42 20H50\"/><path class=\"m\" d=\"M28 6V14M28 42V50M6 28H14M42 28H50\"/><path class=\"m\" d=\"M36 6V14M36 42V50M6 36H14M42 36H50\"/><rect class=\"l\" x=\"14\" y=\"14\" width=\"28\" height=\"28\" rx=\"4\"/><rect class=\"f\" x=\"21\" y=\"21\" width=\"14\" height=\"14\" rx=\"2\" opacity=\"0.35\"/><circle class=\"m\" cx=\"18\" cy=\"18\" r=\"1\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">Core I3</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Gen</tspan></text></svg>",
  "cpu.intel.core.i5": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M20 6V14M20 42V50M6 20H14M42 20H50\"/><path class=\"m\" d=\"M28 6V14M28 42V50M6 28H14M42 28H50\"/><path class=\"m\" d=\"M36 6V14M36 42V50M6 36H14M42 36H50\"/><rect class=\"l\" x=\"14\" y=\"14\" width=\"28\" height=\"28\" rx=\"4\"/><rect class=\"f\" x=\"21\" y=\"21\" width=\"14\" height=\"14\" rx=\"2\" opacity=\"0.35\"/><circle class=\"m\" cx=\"18\" cy=\"18\" r=\"1\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">Core I5</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Gen</tspan></text></svg>",
  "cpu.intel.core.i7": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M20 6V14M20 42V50M6 20H14M42 20H50\"/><path class=\"m\" d=\"M28 6V14M28 42V50M6 28H14M42 28H50\"/><path class=\"m\" d=\"M36 6V14M36 42V50M6 36H14M42 36H50\"/><rect class=\"l\" x=\"14\" y=\"14\" width=\"28\" height=\"28\" rx=\"4\"/><rect class=\"f\" x=\"21\" y=\"21\" width=\"14\" height=\"14\" rx=\"2\" opacity=\"0.35\"/><circle class=\"m\" cx=\"18\" cy=\"18\" r=\"1\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">Core I7</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Gen</tspan></text></svg>",
  "cpu.intel.core.i9": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M20 6V14M20 42V50M6 20H14M42 20H50\"/><path class=\"m\" d=\"M28 6V14M28 42V50M6 28H14M42 28H50\"/><path class=\"m\" d=\"M36 6V14M36 42V50M6 36H14M42 36H50\"/><rect class=\"l\" x=\"14\" y=\"14\" width=\"28\" height=\"28\" rx=\"4\"/><rect class=\"f\" x=\"21\" y=\"21\" width=\"14\" height=\"14\" rx=\"2\" opacity=\"0.35\"/><circle class=\"m\" cx=\"18\" cy=\"18\" r=\"1\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">Core I9</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Gen</tspan></text></svg>",
  "cpu.intel.core.ultra": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M20 6V14M20 42V50M6 20H14M42 20H50\"/><path class=\"m\" d=\"M28 6V14M28 42V50M6 28H14M42 28H50\"/><path class=\"m\" d=\"M36 6V14M36 42V50M6 36H14M42 36H50\"/><rect class=\"l\" x=\"14\" y=\"14\" width=\"28\" height=\"28\" rx=\"4\"/><rect class=\"f\" x=\"21\" y=\"21\" width=\"14\" height=\"14\" rx=\"2\" opacity=\"0.35\"/><circle class=\"m\" cx=\"18\" cy=\"18\" r=\"1\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"30\" font-size=\"7\">Ultra 7</tspan></text></svg>",
  "cpu.intel.celeron": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M20 6V14M20 42V50M6 20H14M42 20H50\"/><path class=\"m\" d=\"M28 6V14M28 42V50M6 28H14M42 28H50\"/><path class=\"m\" d=\"M36 6V14M36 42V50M6 36H14M42 36H50\"/><rect class=\"l\" x=\"14\" y=\"14\" width=\"28\" height=\"28\" rx=\"4\"/><rect class=\"f\" x=\"21\" y=\"21\" width=\"14\" height=\"14\" rx=\"2\" opacity=\"0.35\"/><circle class=\"m\" cx=\"18\" cy=\"18\" r=\"1\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"30\" font-size=\"7\">Celeron</tspan></text></svg>",
  "cpu.intel.pentium": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M20 6V14M20 42V50M6 20H14M42 20H50\"/><path class=\"m\" d=\"M28 6V14M28 42V50M6 28H14M42 28H50\"/><path class=\"m\" d=\"M36 6V14M36 42V50M6 36H14M42 36H50\"/><rect class=\"l\" x=\"14\" y=\"14\" width=\"28\" height=\"28\" rx=\"4\"/><rect class=\"f\" x=\"21\" y=\"21\" width=\"14\" height=\"14\" rx=\"2\" opacity=\"0.35\"/><circle class=\"m\" cx=\"18\" cy=\"18\" r=\"1\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"30\" font-size=\"7\">Pentium</tspan></text></svg>",
  "cpu.amd.ryzen.3": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M20 6V14M20 42V50M6 20H14M42 20H50\"/><path class=\"m\" d=\"M28 6V14M28 42V50M6 28H14M42 28H50\"/><path class=\"m\" d=\"M36 6V14M36 42V50M6 36H14M42 36H50\"/><rect class=\"l\" x=\"12\" y=\"12\" width=\"32\" height=\"32\" rx=\"6\"/><rect class=\"f\" x=\"19\" y=\"19\" width=\"18\" height=\"18\" rx=\"4\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">Ryzen 3</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Series</tspan></text></svg>",
  "cpu.amd.ryzen.5": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M20 6V14M20 42V50M6 20H14M42 20H50\"/><path class=\"m\" d=\"M28 6V14M28 42V50M6 28H14M42 28H50\"/><path class=\"m\" d=\"M36 6V14M36 42V50M6 36H14M42 36H50\"/><rect class=\"l\" x=\"12\" y=\"12\" width=\"32\" height=\"32\" rx=\"6\"/><rect class=\"f\" x=\"19\" y=\"19\" width=\"18\" height=\"18\" rx=\"4\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">Ryzen 5</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Series</tspan></text></svg>",
  "cpu.amd.ryzen.7": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M20 6V14M20 42V50M6 20H14M42 20H50\"/><path class=\"m\" d=\"M28 6V14M28 42V50M6 28H14M42 28H50\"/><path class=\"m\" d=\"M36 6V14M36 42V50M6 36H14M42 36H50\"/><rect class=\"l\" x=\"12\" y=\"12\" width=\"32\" height=\"32\" rx=\"6\"/><rect class=\"f\" x=\"19\" y=\"19\" width=\"18\" height=\"18\" rx=\"4\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">Ryzen 7</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Series</tspan></text></svg>",
  "cpu.amd.ryzen.9": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M20 6V14M20 42V50M6 20H14M42 20H50\"/><path class=\"m\" d=\"M28 6V14M28 42V50M6 28H14M42 28H50\"/><path class=\"m\" d=\"M36 6V14M36 42V50M6 36H14M42 36H50\"/><rect class=\"l\" x=\"12\" y=\"12\" width=\"32\" height=\"32\" rx=\"6\"/><rect class=\"f\" x=\"19\" y=\"19\" width=\"18\" height=\"18\" rx=\"4\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">Ryzen 9</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Series</tspan></text></svg>",
  "cpu.amd.ryzen.ai": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M20 6V14M20 42V50M6 20H14M42 20H50\"/><path class=\"m\" d=\"M28 6V14M28 42V50M6 28H14M42 28H50\"/><path class=\"m\" d=\"M36 6V14M36 42V50M6 36H14M42 36H50\"/><rect class=\"l\" x=\"12\" y=\"12\" width=\"32\" height=\"32\" rx=\"6\"/><rect class=\"f\" x=\"19\" y=\"19\" width=\"18\" height=\"18\" rx=\"4\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">Ryzen</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">AI</tspan></text></svg>",
  "cpu.amd.threadripper": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M8 4V12M8 44V52\"/><path class=\"m\" d=\"M4 8H12M44 8H52\"/><path class=\"m\" d=\"M16 4V12M16 44V52\"/><path class=\"m\" d=\"M4 16H12M44 16H52\"/><path class=\"m\" d=\"M24 4V12M24 44V52\"/><path class=\"m\" d=\"M4 24H12M44 24H52\"/><path class=\"m\" d=\"M32 4V12M32 44V52\"/><path class=\"m\" d=\"M4 32H12M44 32H52\"/><path class=\"m\" d=\"M40 4V12M40 44V52\"/><path class=\"m\" d=\"M4 40H12M44 40H52\"/><path class=\"m\" d=\"M48 4V12M48 44V52\"/><path class=\"m\" d=\"M4 48H12M44 48H52\"/><rect class=\"l\" x=\"8\" y=\"16\" width=\"40\" height=\"24\" rx=\"4\"/><rect class=\"f\" x=\"14\" y=\"22\" width=\"28\" height=\"12\" rx=\"2\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"6.5\">Threadripper</tspan><tspan x=\"28\" y=\"34\" font-size=\"5\" fill=\"var(--art-line)\">PRO</tspan></text></svg>",
  "cpu.amd.athlon": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"m\" d=\"M20 6V14M20 42V50M6 20H14M42 20H50\"/><path class=\"m\" d=\"M28 6V14M28 42V50M6 28H14M42 28H50\"/><path class=\"m\" d=\"M36 6V14M36 42V50M6 36H14M42 36H50\"/><rect class=\"l\" x=\"12\" y=\"12\" width=\"32\" height=\"32\" rx=\"6\"/><rect class=\"f\" x=\"19\" y=\"19\" width=\"18\" height=\"18\" rx=\"4\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"30\" font-size=\"7\">Athlon</tspan></text></svg>",
  "cpu.qualcomm.snapdragon.7c": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"10\" y=\"14\" width=\"36\" height=\"28\" rx=\"5\"/><rect class=\"f\" x=\"16\" y=\"20\" width=\"24\" height=\"16\" rx=\"3\" opacity=\"0.35\"/><path class=\"m\" d=\"M14 32H42\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"6.5\">SD</tspan><tspan x=\"28\" y=\"34\" font-size=\"5\" fill=\"var(--art-line)\">7c</tspan></text></svg>",
  "cpu.qualcomm.snapdragon.8cx": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"10\" y=\"14\" width=\"36\" height=\"28\" rx=\"5\"/><rect class=\"f\" x=\"16\" y=\"20\" width=\"24\" height=\"16\" rx=\"3\" opacity=\"0.35\"/><path class=\"m\" d=\"M14 32H42\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"6.5\">SD</tspan><tspan x=\"28\" y=\"34\" font-size=\"5\" fill=\"var(--art-line)\">8cx</tspan></text></svg>",
  "cpu.qualcomm.snapdragon.x.plus": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"10\" y=\"14\" width=\"36\" height=\"28\" rx=\"5\"/><rect class=\"f\" x=\"16\" y=\"20\" width=\"24\" height=\"16\" rx=\"3\" opacity=\"0.35\"/><path class=\"m\" d=\"M14 32H42\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"6.5\">X</tspan><tspan x=\"28\" y=\"34\" font-size=\"5\" fill=\"var(--art-line)\">Plus</tspan></text></svg>",
  "cpu.qualcomm.snapdragon.x.elite": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"10\" y=\"14\" width=\"36\" height=\"28\" rx=\"5\"/><rect class=\"f\" x=\"16\" y=\"20\" width=\"24\" height=\"16\" rx=\"3\" opacity=\"0.35\"/><path class=\"m\" d=\"M14 32H42\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"6.5\">X</tspan><tspan x=\"28\" y=\"34\" font-size=\"5\" fill=\"var(--art-line)\">Elite</tspan></text></svg>",
  "cpu.arm.generic": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"13\" y=\"13\" width=\"30\" height=\"30\" rx=\"8\"/><path class=\"f\" d=\"M20 20h16v16H20z\" opacity=\"0.35\"/><path class=\"m\" d=\"M16 28h6M34 28h6M28 16v6M28 34v6\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"30\" font-size=\"7\">ARM</tspan></text></svg>",
  "cpu.apple.m1": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"30\" font-size=\"7\">M1</tspan></text></svg>",
  "cpu.apple.m1.pro": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">M1</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Pro</tspan></text></svg>",
  "cpu.apple.m1.max": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">M1</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Max</tspan></text></svg>",
  "cpu.apple.m1.ultra": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">M1</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Ultra</tspan></text></svg>",
  "cpu.apple.m2": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"30\" font-size=\"7\">M2</tspan></text></svg>",
  "cpu.apple.m2.pro": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">M2</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Pro</tspan></text></svg>",
  "cpu.apple.m2.max": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">M2</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Max</tspan></text></svg>",
  "cpu.apple.m2.ultra": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">M2</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Ultra</tspan></text></svg>",
  "cpu.apple.m3": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"30\" font-size=\"7\">M3</tspan></text></svg>",
  "cpu.apple.m3.pro": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">M3</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Pro</tspan></text></svg>",
  "cpu.apple.m3.max": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">M3</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Max</tspan></text></svg>",
  "cpu.apple.m3.ultra": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">M3</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Ultra</tspan></text></svg>",
  "cpu.apple.m4": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"30\" font-size=\"7\">M4</tspan></text></svg>",
  "cpu.apple.m4.pro": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">M4</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Pro</tspan></text></svg>",
  "cpu.apple.m4.max": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">M4</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Max</tspan></text></svg>",
  "cpu.apple.m4.ultra": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"11\" y=\"11\" width=\"34\" height=\"34\" rx=\"9\"/><rect class=\"f\" x=\"17\" y=\"17\" width=\"22\" height=\"22\" rx=\"6\" opacity=\"0.35\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"26\" font-size=\"7\">M4</tspan><tspan x=\"28\" y=\"34\" font-size=\"5.5\" fill=\"var(--art-line)\">Ultra</tspan></text></svg>",
  "gpu.generic": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"3\" y=\"11\" width=\"50\" height=\"29\" rx=\"3\"/><circle class=\"l\" cx=\"28\" cy=\"25.5\" r=\"10\"/><circle class=\"f\" cx=\"28\" cy=\"25.5\" r=\"2.6\"/><path class=\"l\" d=\"M9 40V46H29V40\"/><path class=\"m\" d=\"M36 46H48\"/></svg>",
  "gpu.nvidia.geforce": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"3\" y=\"11\" width=\"50\" height=\"29\" rx=\"3\"/><circle class=\"l\" cx=\"19\" cy=\"25.5\" r=\"9\"/><path class=\"l\" d=\"M19 22V18.5\" transform=\"rotate(0 19 25.5)\"/><path class=\"l\" d=\"M19 22V18.5\" transform=\"rotate(120 19 25.5)\"/><path class=\"l\" d=\"M19 22V18.5\" transform=\"rotate(240 19 25.5)\"/><circle class=\"f\" cx=\"19\" cy=\"25.5\" r=\"2.4\"/><circle class=\"l\" cx=\"38\" cy=\"25.5\" r=\"9\"/><path class=\"l\" d=\"M38 22V18.5\" transform=\"rotate(0 38 25.5)\"/><path class=\"l\" d=\"M38 22V18.5\" transform=\"rotate(120 38 25.5)\"/><path class=\"l\" d=\"M38 22V18.5\" transform=\"rotate(240 38 25.5)\"/><circle class=\"f\" cx=\"38\" cy=\"25.5\" r=\"2.4\"/><path class=\"l\" d=\"M9 40V46H29V40\"/><path class=\"m\" d=\"M36 46H48\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"49\" font-size=\"5\">RTX</tspan></text></svg>",
  "gpu.nvidia.rtx": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"3\" y=\"11\" width=\"50\" height=\"29\" rx=\"3\"/><circle class=\"l\" cx=\"19\" cy=\"25.5\" r=\"9\"/><path class=\"l\" d=\"M19 22V18.5\" transform=\"rotate(0 19 25.5)\"/><path class=\"l\" d=\"M19 22V18.5\" transform=\"rotate(120 19 25.5)\"/><path class=\"l\" d=\"M19 22V18.5\" transform=\"rotate(240 19 25.5)\"/><circle class=\"f\" cx=\"19\" cy=\"25.5\" r=\"2.4\"/><circle class=\"l\" cx=\"38\" cy=\"25.5\" r=\"9\"/><path class=\"l\" d=\"M38 22V18.5\" transform=\"rotate(0 38 25.5)\"/><path class=\"l\" d=\"M38 22V18.5\" transform=\"rotate(120 38 25.5)\"/><path class=\"l\" d=\"M38 22V18.5\" transform=\"rotate(240 38 25.5)\"/><circle class=\"f\" cx=\"38\" cy=\"25.5\" r=\"2.4\"/><path class=\"l\" d=\"M9 40V46H29V40\"/><path class=\"m\" d=\"M36 46H48\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"49\" font-size=\"5\">RTX</tspan></text></svg>",
  "gpu.amd.radeon": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"3\" y=\"11\" width=\"50\" height=\"29\" rx=\"3\"/><circle class=\"l\" cx=\"19\" cy=\"25.5\" r=\"9\"/><path class=\"l\" d=\"M19 22V18.5\" transform=\"rotate(0 19 25.5)\"/><path class=\"l\" d=\"M19 22V18.5\" transform=\"rotate(120 19 25.5)\"/><path class=\"l\" d=\"M19 22V18.5\" transform=\"rotate(240 19 25.5)\"/><circle class=\"f\" cx=\"19\" cy=\"25.5\" r=\"2.4\"/><circle class=\"l\" cx=\"38\" cy=\"25.5\" r=\"9\"/><path class=\"l\" d=\"M38 22V18.5\" transform=\"rotate(0 38 25.5)\"/><path class=\"l\" d=\"M38 22V18.5\" transform=\"rotate(120 38 25.5)\"/><path class=\"l\" d=\"M38 22V18.5\" transform=\"rotate(240 38 25.5)\"/><circle class=\"f\" cx=\"38\" cy=\"25.5\" r=\"2.4\"/><path class=\"l\" d=\"M9 40V46H29V40\"/><path class=\"m\" d=\"M36 46H48\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"49\" font-size=\"5\">Radeon</tspan></text></svg>",
  "gpu.intel.arc": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"3\" y=\"11\" width=\"50\" height=\"29\" rx=\"3\"/><circle class=\"l\" cx=\"19\" cy=\"25.5\" r=\"9\"/><path class=\"l\" d=\"M19 22V18.5\" transform=\"rotate(0 19 25.5)\"/><path class=\"l\" d=\"M19 22V18.5\" transform=\"rotate(120 19 25.5)\"/><path class=\"l\" d=\"M19 22V18.5\" transform=\"rotate(240 19 25.5)\"/><circle class=\"f\" cx=\"19\" cy=\"25.5\" r=\"2.4\"/><circle class=\"l\" cx=\"38\" cy=\"25.5\" r=\"9\"/><path class=\"l\" d=\"M38 22V18.5\" transform=\"rotate(0 38 25.5)\"/><path class=\"l\" d=\"M38 22V18.5\" transform=\"rotate(120 38 25.5)\"/><path class=\"l\" d=\"M38 22V18.5\" transform=\"rotate(240 38 25.5)\"/><circle class=\"f\" cx=\"38\" cy=\"25.5\" r=\"2.4\"/><path class=\"l\" d=\"M9 40V46H29V40\"/><path class=\"m\" d=\"M36 46H48\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"49\" font-size=\"5\">Arc</tspan></text></svg>",
  "gpu.integrated": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"8\" y=\"10\" width=\"40\" height=\"36\" rx=\"4\"/><rect class=\"f\" x=\"14\" y=\"16\" width=\"28\" height=\"24\" rx=\"3\" opacity=\"0.25\"/><rect class=\"l\" x=\"22\" y=\"32\" width=\"12\" height=\"8\" rx=\"2\"/><path class=\"m\" d=\"M18 20h20M18 24h14\"/><text fill=\"var(--art-accent)\" font-family=\"Segoe UI, sans-serif\" font-weight=\"600\" text-anchor=\"middle\"><tspan x=\"28\" y=\"22\" font-size=\"6\">iGPU</tspan><tspan x=\"28\" y=\"30\" font-size=\"5\" fill=\"var(--art-line)\">on die</tspan></text></svg>",
  "device.laptop": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"7\" y=\"7\" width=\"34\" height=\"24\" rx=\"3\"/><path class=\"l\" d=\"M3 35H45L42 41H6Z\"/><rect class=\"m\" x=\"24\" y=\"38\" width=\"8\" height=\"2\" rx=\"1\"/></svg>",
  "device.laptop.gaming": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"7\" y=\"7\" width=\"34\" height=\"24\" rx=\"3\"/><path class=\"l\" d=\"M3 35H45L42 41H6Z\"/><path class=\"f\" d=\"M38 16h8v6h-8z\" opacity=\"0.45\"/><rect class=\"m\" x=\"24\" y=\"38\" width=\"8\" height=\"2\" rx=\"1\"/></svg>",
  "device.desktop": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"14\" y=\"6\" width=\"28\" height=\"40\" rx=\"3\"/><circle class=\"m\" cx=\"28\" cy=\"18\" r=\"3\"/><path class=\"m\" d=\"M18 30h20\"/><rect class=\"f\" x=\"20\" y=\"42\" width=\"16\" height=\"4\" rx=\"1\" opacity=\"0.4\"/></svg>",
  "device.aio": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"8\" y=\"6\" width=\"40\" height=\"30\" rx=\"4\"/><path class=\"l\" d=\"M22 36h12v6H22z\"/><path class=\"m\" d=\"M18 46h20\"/></svg>",
  "device.tablet": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"12\" y=\"6\" width=\"32\" height=\"44\" rx=\"4\"/><circle class=\"f\" cx=\"28\" cy=\"46\" r=\"1.5\"/></svg>",
  "device.macbook": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"7\" y=\"9\" width=\"34\" height=\"22\" rx=\"3\"/><path class=\"l\" d=\"M3 35H45L42 41H6Z\"/><rect class=\"f\" x=\"26\" y=\"14\" width=\"12\" height=\"12\" rx=\"6\" opacity=\"0.3\"/></svg>",
  "device.pc.generic": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"6\" y=\"8\" width=\"44\" height=\"32\" rx=\"4\"/><path class=\"m\" d=\"M14 20h28M14 26h20\"/><circle class=\"m\" cx=\"42\" cy=\"36\" r=\"2\"/></svg>",
  "memory.ddr4": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"3\" y=\"15\" width=\"50\" height=\"24\" rx=\"3\"/><rect class=\"f\" x=\"8\" y=\"20\" width=\"7\" height=\"10\" rx=\"1.5\"/><rect class=\"f\" x=\"17\" y=\"20\" width=\"7\" height=\"10\" rx=\"1.5\"/><rect class=\"f\" x=\"26\" y=\"20\" width=\"7\" height=\"10\" rx=\"1.5\"/><rect class=\"f\" x=\"35\" y=\"20\" width=\"7\" height=\"10\" rx=\"1.5\"/><path class=\"m\" d=\"M8 34H30\"/><text x=\"28\" y=\"49\" text-anchor=\"middle\" fill=\"var(--art-line)\" font-size=\"6\" font-weight=\"600\">DDR4</text></svg>",
  "memory.ddr5": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"3\" y=\"15\" width=\"50\" height=\"24\" rx=\"3\"/><rect class=\"f\" x=\"8\" y=\"20\" width=\"6\" height=\"10\" rx=\"1.5\"/><rect class=\"f\" x=\"15.5\" y=\"20\" width=\"6\" height=\"10\" rx=\"1.5\"/><rect class=\"f\" x=\"23\" y=\"20\" width=\"6\" height=\"10\" rx=\"1.5\"/><rect class=\"f\" x=\"30.5\" y=\"20\" width=\"6\" height=\"10\" rx=\"1.5\"/><rect class=\"f\" x=\"38\" y=\"20\" width=\"6\" height=\"10\" rx=\"1.5\"/><path class=\"m\" d=\"M8 34H34\"/><text x=\"28\" y=\"49\" text-anchor=\"middle\" fill=\"var(--art-line)\" font-size=\"6\" font-weight=\"600\">DDR5</text></svg>",
  "memory.sodimm": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"6\" y=\"18\" width=\"44\" height=\"20\" rx=\"3\"/><rect class=\"f\" x=\"10\" y=\"22\" width=\"8\" height=\"8\" rx=\"1.5\"/><rect class=\"f\" x=\"22\" y=\"22\" width=\"8\" height=\"8\" rx=\"1.5\"/><path class=\"m\" d=\"M34 26h12\"/><text x=\"28\" y=\"48\" text-anchor=\"middle\" fill=\"var(--art-line)\" font-size=\"6\" font-weight=\"600\">SO-DIMM</text></svg>",
  "memory.lpddr": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"10\" y=\"16\" width=\"36\" height=\"24\" rx=\"4\"/><path class=\"md\" d=\"M16 22h24M16 28h24M16 34h24\"/><text x=\"28\" y=\"48\" text-anchor=\"middle\" fill=\"var(--art-line)\" font-size=\"6\" font-weight=\"600\">LPDDR</text></svg>",
  "storage.nvme": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"8\" y=\"20\" width=\"40\" height=\"16\" rx=\"2\"/><rect class=\"f\" x=\"12\" y=\"24\" width=\"20\" height=\"8\" rx=\"1\" opacity=\"0.35\"/><path class=\"m\" d=\"M38 28h6\"/><circle class=\"m\" cx=\"46\" cy=\"28\" r=\"1.5\"/></svg>",
  "storage.sata.ssd": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"6\" y=\"18\" width=\"44\" height=\"20\" rx=\"3\"/><path class=\"m\" d=\"M12 28h32\"/><rect class=\"f\" x=\"40\" y=\"24\" width=\"4\" height=\"8\" rx=\"1\"/></svg>",
  "storage.hdd": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"6\" y=\"14\" width=\"44\" height=\"28\" rx=\"3\"/><circle class=\"l\" cx=\"28\" cy=\"28\" r=\"9\"/><circle class=\"f\" cx=\"28\" cy=\"28\" r=\"2\"/><path class=\"m\" d=\"M14 38h28\"/></svg>",
  "storage.usb.external": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"10\" y=\"22\" width=\"30\" height=\"18\" rx=\"3\"/><path class=\"l\" d=\"M40 28h8v6h-8z\"/><path class=\"m\" d=\"M16 28h18\"/></svg>",
  "battery.full": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"4\" y=\"16\" width=\"42\" height=\"24\" rx=\"5\"/><rect class=\"f\" x=\"46\" y=\"24\" width=\"5\" height=\"8\" rx=\"1.5\"/><rect class=\"f\" x=\"9\" y=\"21\" width=\"38\" height=\"14\" rx=\"2\" opacity=\"0.45\"/></svg>",
  "battery.mid": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"4\" y=\"16\" width=\"42\" height=\"24\" rx=\"5\"/><rect class=\"f\" x=\"46\" y=\"24\" width=\"5\" height=\"8\" rx=\"1.5\"/><rect class=\"f\" x=\"9\" y=\"21\" width=\"24\" height=\"14\" rx=\"2\" opacity=\"0.45\"/></svg>",
  "battery.low": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"4\" y=\"16\" width=\"42\" height=\"24\" rx=\"5\"/><rect class=\"f\" x=\"46\" y=\"24\" width=\"5\" height=\"8\" rx=\"1.5\"/><rect class=\"f\" x=\"9\" y=\"21\" width=\"12\" height=\"14\" rx=\"2\" opacity=\"0.45\"/></svg>",
  "battery.charging": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"4\" y=\"16\" width=\"42\" height=\"24\" rx=\"5\"/><rect class=\"f\" x=\"46\" y=\"24\" width=\"5\" height=\"8\" rx=\"1.5\"/><rect class=\"f\" x=\"9\" y=\"21\" width=\"24\" height=\"14\" rx=\"2\" opacity=\"0.45\"/><path class=\"f\" d=\"M30 22l4 4-6 6-2-4z\"/></svg>",
  "battery.none": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"4\" y=\"16\" width=\"42\" height=\"24\" rx=\"5\"/><rect class=\"f\" x=\"46\" y=\"24\" width=\"5\" height=\"8\" rx=\"1.5\"/><path class=\"md\" d=\"M12 28h32\"/></svg>",
  "display.laptop": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"8\" y=\"8\" width=\"40\" height=\"28\" rx=\"3\"/><path class=\"m\" d=\"M18 40h20\"/></svg>",
  "display.external": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"6\" y=\"10\" width=\"44\" height=\"30\" rx=\"3\"/><path class=\"l\" d=\"M22 40h12v6H22z\"/><path class=\"m\" d=\"M16 48h24\"/></svg>",
  "display.multiple": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"4\" y=\"14\" width=\"28\" height=\"22\" rx=\"2\"/><rect class=\"l\" x=\"24\" y=\"10\" width=\"28\" height=\"26\" rx=\"2\"/><path class=\"m\" d=\"M14 40h8M34 40h8\"/></svg>",
  "camera.webcam": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><circle class=\"l\" cx=\"28\" cy=\"22\" r=\"17\"/><circle class=\"m\" cx=\"28\" cy=\"22\" r=\"10\"/><circle class=\"f\" cx=\"28\" cy=\"22\" r=\"5.5\"/><circle cx=\"30.5\" cy=\"19.5\" r=\"1.6\" fill=\"var(--art-fill)\"/><path class=\"l\" d=\"M28 39V46M17 49H39\"/></svg>",
  "camera.builtin": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><rect class=\"l\" x=\"10\" y=\"12\" width=\"36\" height=\"26\" rx=\"4\"/><circle class=\"f\" cx=\"28\" cy=\"25\" r=\"4\" opacity=\"0.5\"/><path class=\"m\" d=\"M22 42h12\"/></svg>",
  "camera.none": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><circle class=\"l\" cx=\"28\" cy=\"28\" r=\"16\"/><path class=\"md\" d=\"M16 16l24 24\"/></svg>",
  "os.windows11": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"f\" d=\"M7 10L25 7V25H7Z\"/><path class=\"l\" d=\"M30 6.5L49 4V25H30Z\"/><path class=\"l\" d=\"M7 30H25V48.5L7 46Z\"/><path class=\"l\" d=\"M30 30H49V52L30 49.5Z\"/></svg>",
  "os.windows10": "<svg class=\"ill\" viewBox=\"0 0 56 56\" aria-hidden=\"true\" xmlns=\"http://www.w3.org/2000/svg\"><path class=\"f\" d=\"M7 10L25 7V25H7Z\" opacity=\"0.5\"/><path class=\"l\" d=\"M30 6.5L49 4V25H30Z\"/><path class=\"l\" d=\"M7 30H25V48.5L7 46Z\"/><path class=\"l\" d=\"M30 30H49V52L30 49.5Z\"/><text x=\"28\" y=\"30\" text-anchor=\"middle\" fill=\"var(--art-line)\" font-size=\"5.5\" font-weight=\"600\">10</text></svg>"
};

function esc(s) {
  return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
}

function wrap(inner, w, h) {
  w = w || 56;
  h = h || 56;
  return (
    '<svg class="ill" viewBox="0 0 ' +
    w +
    ' ' +
    h +
    '" aria-hidden="true" xmlns="http://www.w3.org/2000/svg">' +
    inner +
    '</svg>'
  );
}

function text(cx, cy, line1, line2, sz1, sz2) {
  sz1 = sz1 || 7;
  sz2 = sz2 || 5.5;
  const y1 = line2 ? cy - 2 : cy + 2;
  const y2 = cy + 6;
  let t =
    '<text fill="var(--art-accent)" font-family="Segoe UI, sans-serif" font-weight="600" text-anchor="middle">';
  t +=
    '<tspan x="' +
    cx +
    '" y="' +
    y1 +
    '" font-size="' +
    sz1 +
    '">' +
    esc(line1) +
    '</tspan>';
  if (line2) {
    t +=
      '<tspan x="' +
      cx +
      '" y="' +
      y2 +
      '" font-size="' +
      sz2 +
      '" fill="var(--art-line)">' +
      esc(line2) +
      '</tspan>';
  }
  return t + '</text>';
}

function pinsIntel() {
  return (
    '<path class="m" d="M20 6V14M20 42V50M6 20H14M42 20H50"/>' +
    '<path class="m" d="M28 6V14M28 42V50M6 28H14M42 28H50"/>' +
    '<path class="m" d="M36 6V14M36 42V50M6 36H14M42 36H50"/>'
  );
}

function cpuIntel(line1, line2) {
  return wrap(
    pinsIntel() +
      '<rect class="l" x="14" y="14" width="28" height="28" rx="4"/>' +
      '<rect class="f" x="21" y="21" width="14" height="14" rx="2" opacity="0.35"/>' +
      '<circle class="m" cx="18" cy="18" r="1"/>' +
      text(28, 28, line1, line2)
  );
}

function cpuAmd(line1, line2) {
  return wrap(
    pinsIntel() +
      '<rect class="l" x="12" y="12" width="32" height="32" rx="6"/>' +
      '<rect class="f" x="19" y="19" width="18" height="18" rx="4" opacity="0.35"/>' +
      text(28, 28, line1, line2)
  );
}

function cpuWide(line1, line2) {
  let pins = '';
  for (let i = 8; i <= 48; i += 8) {
    pins += '<path class="m" d="M' + i + ' 4V12M' + i + ' 44V52"/>';
    pins += '<path class="m" d="M4 ' + i + 'H12M44 ' + i + 'H52"/>';
  }
  return wrap(
    pins +
      '<rect class="l" x="8" y="16" width="40" height="24" rx="4"/>' +
      '<rect class="f" x="14" y="22" width="28" height="12" rx="2" opacity="0.35"/>' +
      text(28, 28, line1, line2, 6.5, 5)
  );
}

function cpuMobile(line1, line2) {
  return wrap(
    '<rect class="l" x="10" y="14" width="36" height="28" rx="5"/>' +
      '<rect class="f" x="16" y="20" width="24" height="16" rx="3" opacity="0.35"/>' +
      '<path class="m" d="M14 32H42"/>' +
      text(28, 28, line1, line2, 6.5, 5)
  );
}

function cpuApple(line1, line2) {
  return wrap(
    '<rect class="l" x="11" y="11" width="34" height="34" rx="9"/>' +
      '<rect class="f" x="17" y="17" width="22" height="22" rx="6" opacity="0.35"/>' +
      text(28, 28, line1, line2)
  );
}

function cpuArm(line1, line2) {
  return wrap(
    '<rect class="l" x="13" y="13" width="30" height="30" rx="8"/>' +
      '<path class="f" d="M20 20h16v16H20z" opacity="0.35"/>' +
      '<path class="m" d="M16 28h6M34 28h6M28 16v6M28 34v6"/>' +
      text(28, 28, line1, line2)
  );
}

function gpuCard(fans, label) {
  let inner = '<rect class="l" x="3" y="11" width="50" height="29" rx="3"/>';
  if (fans === 2) {
    inner +=
      '<circle class="l" cx="19" cy="25.5" r="9"/><path class="l" d="M19 22V18.5" transform="rotate(0 19 25.5)"/><path class="l" d="M19 22V18.5" transform="rotate(120 19 25.5)"/><path class="l" d="M19 22V18.5" transform="rotate(240 19 25.5)"/><circle class="f" cx="19" cy="25.5" r="2.4"/>';
    inner +=
      '<circle class="l" cx="38" cy="25.5" r="9"/><path class="l" d="M38 22V18.5" transform="rotate(0 38 25.5)"/><path class="l" d="M38 22V18.5" transform="rotate(120 38 25.5)"/><path class="l" d="M38 22V18.5" transform="rotate(240 38 25.5)"/><circle class="f" cx="38" cy="25.5" r="2.4"/>';
  } else {
    inner +=
      '<circle class="l" cx="28" cy="25.5" r="10"/><circle class="f" cx="28" cy="25.5" r="2.6"/>';
  }
  inner += '<path class="l" d="M9 40V46H29V40"/><path class="m" d="M36 46H48"/>';
  if (label) inner += text(28, 47, label, null, 5);
  return wrap(inner);
}

function gpuIntegrated() {
  return wrap(
    '<rect class="l" x="8" y="10" width="40" height="36" rx="4"/>' +
      '<rect class="f" x="14" y="16" width="28" height="24" rx="3" opacity="0.25"/>' +
      '<rect class="l" x="22" y="32" width="12" height="8" rx="2"/>' +
      '<path class="m" d="M18 20h20M18 24h14"/>' +
      text(28, 24, 'iGPU', 'on die', 6, 5)
  );
}

function makeCpuArt(opts) {
  opts = opts || {};
  const v = String(opts.vendor || '').toLowerCase();
  const fam = opts.family || '';
  const gen = opts.generation || '';
  if (v.indexOf('intel') >= 0) return cpuIntel(fam || 'Core i7', gen || 'Gen');
  if (v.indexOf('amd') >= 0) return cpuAmd(fam || 'Ryzen 7', gen || 'Series');
  if (v.indexOf('apple') >= 0) return cpuApple(fam || 'M2', gen);
  if (v.indexOf('qualcomm') >= 0) return cpuMobile(fam || 'X', gen || 'Elite');
  if (v.indexOf('arm') >= 0) return cpuArm(fam || 'ARM', gen);
  return cpuArm(fam || 'CPU', gen);
}

function makeGpuArt(opts) {
  opts = opts || {};
  const v = String(opts.vendor || '').toLowerCase();
  const name = String(opts.name || '').toLowerCase();
  if (opts.integrated) return gpuIntegrated();
  if (
    v.indexOf('nvidia') >= 0 ||
    name.indexOf('geforce') >= 0 ||
    name.indexOf('rtx') >= 0
  ) {
    return gpuCard(2, 'RTX');
  }
  if (v.indexOf('amd') >= 0 || name.indexOf('radeon') >= 0) {
    return gpuCard(2, 'Radeon');
  }
  if (v.indexOf('intel') >= 0 || name.indexOf('arc') >= 0) {
    return gpuCard(2, 'Arc');
  }
  return gpuCard(1);
}

function norm(s) {
  return String(s || '').toLowerCase();
}

function pickArt(kind, facts) {
  facts = facts || {};
  const n = norm(facts.name);
  const v = norm(facts.vendor);
  const k = norm(kind);
  if (k === 'cpu' || k === 'processor') {
    if (n.indexOf('threadripper') >= 0) return 'cpu.amd.threadripper';
    if (n.indexOf('athlon') >= 0) return 'cpu.amd.athlon';
    if (n.indexOf('ryzen ai') >= 0) return 'cpu.amd.ryzen.ai';
    if (n.indexOf('ryzen 9') >= 0) return 'cpu.amd.ryzen.9';
    if (n.indexOf('ryzen 7') >= 0) return 'cpu.amd.ryzen.7';
    if (n.indexOf('ryzen 5') >= 0) return 'cpu.amd.ryzen.5';
    if (n.indexOf('ryzen 3') >= 0) return 'cpu.amd.ryzen.3';
    if (v.indexOf('amd') >= 0 && n.indexOf('ryzen') >= 0) return 'cpu.amd.ryzen.7';
    if (n.indexOf('core ultra') >= 0 || n.indexOf('ultra 7') >= 0) {
      return 'cpu.intel.core.ultra';
    }
    if (n.indexOf('core i9') >= 0) return 'cpu.intel.core.i9';
    if (n.indexOf('core i7') >= 0) return 'cpu.intel.core.i7';
    if (n.indexOf('core i5') >= 0) return 'cpu.intel.core.i5';
    if (n.indexOf('core i3') >= 0) return 'cpu.intel.core.i3';
    if (n.indexOf('celeron') >= 0) return 'cpu.intel.celeron';
    if (n.indexOf('pentium') >= 0) return 'cpu.intel.pentium';
    if (n.indexOf('x elite') >= 0) return 'cpu.qualcomm.snapdragon.x.elite';
    if (n.indexOf('x plus') >= 0) return 'cpu.qualcomm.snapdragon.x.plus';
    if (n.indexOf('8cx') >= 0) return 'cpu.qualcomm.snapdragon.8cx';
    if (n.indexOf('7c') >= 0) return 'cpu.qualcomm.snapdragon.7c';
    const apple = n.match(/\bm([1-4])(\s+pro|\s+max|\s+ultra)?\b/);
    if (apple || v.indexOf('apple') >= 0) {
      const m = apple ? apple[1] : '2';
      let tier = '';
      if (n.indexOf('ultra') >= 0) tier = '.ultra';
      else if (n.indexOf('max') >= 0) tier = '.max';
      else if (n.indexOf('pro') >= 0) tier = '.pro';
      const key = 'cpu.apple.m' + m + tier;
      if (HARDWARE_ART[key]) return key;
      return 'cpu.apple.m' + m;
    }
    if (v.indexOf('qualcomm') >= 0) return 'cpu.qualcomm.snapdragon.x.elite';
    if (n.indexOf('arm') >= 0 || v.indexOf('arm') >= 0) return 'cpu.arm.generic';
    return 'cpu.generic';
  }
  if (k === 'gpu' || k === 'graphics') {
    if (facts.integrated === true) return 'gpu.integrated';
    if (
      facts.integrated !== false &&
      (n.indexOf('radeon graphics') >= 0 ||
        n.indexOf('uhd') >= 0 ||
        n.indexOf('iris') >= 0 ||
        n.indexOf('vega') >= 0 ||
        /\bgraphics\b/.test(n))
    ) {
      const dedicated =
        facts.dedicated === true ||
        n.indexOf('rtx') >= 0 ||
        n.indexOf('geforce') >= 0 ||
        n.indexOf('laptop gpu') >= 0;
      if (!dedicated) return 'gpu.integrated';
    }
    if (v.indexOf('nvidia') >= 0 || n.indexOf('geforce') >= 0 || n.indexOf('rtx') >= 0) {
      return 'gpu.nvidia.rtx';
    }
    if (v.indexOf('amd') >= 0 || n.indexOf('radeon') >= 0) return 'gpu.amd.radeon';
    if (n.indexOf('arc') >= 0) return 'gpu.intel.arc';
    return 'gpu.generic';
  }
  if (k === 'device') {
    if (n.indexOf('macbook') >= 0) return 'device.macbook';
    if (n.indexOf('gaming') >= 0) return 'device.laptop.gaming';
    if (n.indexOf('tablet') >= 0 || n.indexOf('2-in-1') >= 0) return 'device.tablet';
    if (n.indexOf('all-in-one') >= 0 || n.indexOf('aio') >= 0) return 'device.aio';
    if (n.indexOf('desktop') >= 0 || n.indexOf('tower') >= 0) return 'device.desktop';
    if (n.indexOf('laptop') >= 0) return 'device.laptop';
    return 'device.pc.generic';
  }
  if (k === 'memory') {
    if (n.indexOf('lpddr') >= 0) return 'memory.lpddr';
    if (n.indexOf('so-dimm') >= 0 || n.indexOf('sodimm') >= 0) return 'memory.sodimm';
    if (n.indexOf('ddr5') >= 0) return 'memory.ddr5';
    return 'memory.ddr4';
  }
  if (k === 'storage' || k === 'drive') {
    if (facts.bus === 'usb' || n.indexOf('external') >= 0 || n.indexOf('removable') >= 0) {
      return 'storage.usb.external';
    }
    if (n.indexOf('nvme') >= 0 || n.indexOf('m.2') >= 0) return 'storage.nvme';
    if (n.indexOf('ssd') >= 0) return 'storage.sata.ssd';
    if (n.indexOf('hdd') >= 0 || n.indexOf('hard') >= 0) return 'storage.hdd';
    return 'storage.nvme';
  }
  if (k === 'battery') {
    if (facts.present === false) return 'battery.none';
    if (facts.charging) return 'battery.charging';
    const pct = facts.percent;
    if (pct != null && pct < 25) return 'battery.low';
    if (pct != null && pct < 60) return 'battery.mid';
    return 'battery.full';
  }
  if (k === 'display') {
    if (facts.count > 1) return 'display.multiple';
    if (facts.external) return 'display.external';
    return 'display.laptop';
  }
  if (k === 'camera') {
    if (facts.present === false) return 'camera.none';
    if (facts.builtin !== false) return 'camera.builtin';
    return 'camera.webcam';
  }
  if (k === 'os') {
    if (n.indexOf('11') >= 0) return 'os.windows11';
    return 'os.windows10';
  }
  return 'cpu.generic';
}

  root.HARDWARE_ART = HARDWARE_ART;
  root.makeCpuArt = makeCpuArt;
  root.makeGpuArt = makeGpuArt;
  root.pickArt = pickArt;
})(typeof window !== "undefined" ? window : global);

