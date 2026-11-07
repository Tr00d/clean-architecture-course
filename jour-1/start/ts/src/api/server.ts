import { createApp } from "./app.ts";

const port = 5122;

createApp().listen(port, () => {
  console.log(`Listening on http://localhost:${port}`);
});
