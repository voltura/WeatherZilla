# Vendored jQuery Validation

Version: 1.22.1, distributed under the included MIT license.
Source: https://registry.npmjs.org/jquery-validation/-/jquery-validation-1.22.1.tgz

The four files in `dist` and `LICENSE.md` come from the upstream distribution. The four JavaScript files additionally exclude `%` from the plain credential character alternative, so percent-encoded credentials have only one matching path; this removes an overlapping-alternative backtracking case in upstream 1.22.1. Update minified and unminified copies together. This replaces 1.17.0, including the URL and URL2 regular expressions reported by CodeQL alerts 4, 5 and 6.

Run `npm ci --prefix tests/validation` and `npm test --prefix tests/validation` from the repository root. Tests load the shipped jQuery and ASP.NET unobtrusive integration, exercise both distribution variants, and bound pathological URL validation to 500 ms. Upstream URL2 now uses the URL validator rules with an optional TLD; this application has no URL2 callers.
