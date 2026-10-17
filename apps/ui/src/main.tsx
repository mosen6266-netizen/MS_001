import React from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { PanelErrorBoundary } from "./components/PanelErrorBoundary";
import "./style.css";
const target = document.getElementById("root");
if (!target) throw new Error("root mount target unavailable");
createRoot(target).render(<React.StrictMode><PanelErrorBoundary label="主界面"><App /></PanelErrorBoundary></React.StrictMode>);
