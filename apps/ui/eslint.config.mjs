import eslintParser from "@typescript-eslint/parser";
import typescript from "@typescript-eslint/eslint-plugin";
export default [
 { ignores: ["dist/**", "node_modules/**"] },
 { files: ["src/**/*.{ts,tsx}"], languageOptions: { parser: eslintParser, parserOptions: { ecmaVersion: 2022, sourceType: "module", ecmaFeatures: { jsx: true } } }, plugins: { "@typescript-eslint": typescript }, rules: { ...typescript.configs.recommended.rules, "no-undef": "off" } },
];
