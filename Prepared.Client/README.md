# Webpack Build Setup

This project uses Webpack 5 for bundling JavaScript and CSS assets.

## Prerequisites

- Node.js 18+ and npm

## Installation

First, install the npm dependencies:

```bash
cd Prepared.Client
npm install
```

## Development

Run Webpack in watch mode (rebuilds on file changes):

```bash
npm run dev
```

Or use the webpack dev server:

```bash
npm start
```

## Production Build

Build for production (minified and optimized):

```bash
npm run build
```

## Project Structure

- **Source files**: `src/` directory
  - `layout.js` - Global scripts for all pages
  - `calls.js` - Call dashboard page specific scripts (SignalR, Google Maps)
- **Output**: `wwwroot/dist/` directory
  - Bundled JS files: `[name].bundle.js`
  - Bundled CSS files: `[name].bundle.css`

## Usage in Views

The bundled files are automatically included in `_Layout.cshtml`:
- CSS bundles are loaded in the `<head>`
- JS bundles are loaded before `</body>`
- Page-specific bundles (like `calls.bundle.js`) are conditionally loaded based on the current controller/action

## Adding New Pages

1. Create a new entry file in `src/` (e.g., `src/newpage.js`)
2. Add it to the `entry` object in `webpack.config.js`
3. Update `_Layout.cshtml` to conditionally load the bundle for that page

## Dependencies

- **@microsoft/signalr**: Real-time communication with SignalR hubs
- **bootstrap**: UI framework (loaded via webpack)
- **babel**: JavaScript transpilation for browser compatibility

