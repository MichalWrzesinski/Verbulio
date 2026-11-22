# Design reference

On 2026-11-22 the owner selected the comic treatment shown by the recovered HTML and screenshots. Use the cream panels, printed patterns, black outlines and yellow and red logo. Preserve the reference layout, responsive rules, local fonts and interactions where the current product permits them.

Open the [screen gallery](reference/views/index.html) and [historical clickable prototype](reference/prototype/index.html) in a browser. The snapshot keeps the production candidate's design inputs intact. Compare each implemented view side by side at 375 and 1280 px when a reference exists. Check 320 px for horizontal overflow, long translations and keyboard access. Mapping uses screen intent because the prototype and gallery numbering differ.

| Reference | Use in the current flow |
| --- | --- |
| J-1 landing, test, result and account | Preserve visual structure. The adaptive test has 20 questions, omits feedback during placement and joins the server-verified result to the account. |
| J-2 prompts, feedback and summary | Reuse the comic frames and answer states. Verify the input, accents and retry behavior against the evaluator contract. Add explicit pending synchronization and conflict states. |
| J-3 categories | Reuse the list and short/empty layouts. Count actual distinct words. |
| J-4 profile and settings | Reuse the layout. Use persisted counts and request the password before deleting an account. |
| J-5 portfolio | Route portfolio traffic into ordinary onboarding. Retain the technical page style and supply verified metrics as they become available. |
| K account lifecycle | Reuse the forms and messaging patterns. Add the selected abuse protection and test the seven-day confirmation gate. |

Initial source audit covered the gallery and prototype indexes, landing markup, comic theme, linked brand files and viewport references. Visual, keyboard and assistive-technology acceptance applies to the implementation.

## Landing verification

On 2026-11-22, a local Chrome comparison of the production Angular build against J-1 measured identical element geometry and background at widths of 320, 375 and 1280 px. No horizontal overflow was observed at those widths. The 375 px screenshot matched byte for byte. The runtime CSS adjusts the legal footer selector for the Angular component host. Links use Angular routes and lead to clearly marked pending screens until their features are available.
