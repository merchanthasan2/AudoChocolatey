const fs = require("fs");
const t = fs.readFileSync("tracker.html", "utf8");
const s = t.split("<script>")[1].split("</script>")[0];
new Function(s);
const start = s.indexOf("const TRACKER = ");
const end = s.indexOf("/* ========== END TRACKER DATA ========== */");
const obj = s.slice(start + "const TRACKER = ".length, end).trim().replace(/;$/, "");
const TRACKER = eval("(" + obj + ")");
let w = 0;
let d = 0;
TRACKER.phases.forEach((p) => {
  p.items.forEach((i) => {
    const wt =
      i.status === "done" || i.status === "checked"
        ? 1
        : i.status === "in-progress"
          ? 0.5
          : i.status === "planning"
            ? 0.25
            : 0;
    w += wt;
    d += 1;
  });
});
console.log("phases", TRACKER.phases.length, "items", d, "pct", Math.round((100 * w) / d), "held groups", TRACKER.heldOut.length);
const ids = TRACKER.phases.map((p) => p.id).join(",");
console.log("ids", ids);
