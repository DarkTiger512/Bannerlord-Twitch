import { copyFile } from "node:fs/promises";
import { verifyPackage } from "./verify-package.mjs";
for (const file of ["viewer.html", "config.html", "live-config.html"]) await copyFile("dist/index.html", `dist/${file}`);
verifyPackage("dist");
