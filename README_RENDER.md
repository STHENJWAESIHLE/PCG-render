# Render deployment

This copy is prepared for Render.

## What changed
- Falls back to SQLite automatically on Render.
- Creates `App_Data/uploads` and `App_Data/upload_temp` on startup.
- Seeds demo users automatically.
- Removes hard-coded API keys from `appsettings.json`.
- Adds `render.yaml` for one-click style setup.

## Demo login
- admin@pcg.demo / DemoPcg2026!
- reviewer@pcg.demo / DemoPcg2026!
- manager@pcg.demo / DemoPcg2026!
- external@pcg.demo / DemoPcg2026!
- viewer@pcg.demo / DemoPcg2026!

## Render steps
1. Push these changes to GitHub.
2. In Render, create a new Web Service from the repo.
3. Render should detect `render.yaml`.
4. Add `InvoiceExtraction__OpenAIApiKey` and `Insights__OpenAIApiKey` only if you want AI features enabled.
5. Deploy.

## Important
Because your original public repo exposed OpenAI keys, revoke those keys and create new ones before using the app again.
