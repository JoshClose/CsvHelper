CsvHelper note: this directory powers the API pages. It is a clone of https://github.com/statiqdev/Docable/tree/205f0f6a5c636b0d05d0b031385fd6a620e30ba0 with some edits on top.

---

# Docable

Documentation theme for Statiq Docs.

# Minimum Statiq Docs Version

This theme requires Statiq Docs 1.0.0-beta.2 or later.

Using an earlier commit of the theme may allow the use of an earlier version of Statiq Web (look at the theme `themesettings.yml` file to determine the minimum Statiq Docs version for a given version of the theme).

# Installation

Statiq themes go in a `theme` folder alongside your `input` folder. If your site is inside a git repository, you can add the theme as a git submodule:

```
git submodule add https://github.com/statiqdev/Docable.git theme
```

Alternatively you can clone the theme directly:

```
git clone https://github.com/statiqdev/Docable.git theme
```

Once inside the `theme` folder, Statiq will automatically recognize the theme. If you want to tweak the theme you can edit files directly in the `theme` folder or copy them to your `input` folder and edit them there.

# Settings

## Global

These are theme-specific settings and can be set in a settings file (in addition to any Statiq Docs settings or [Statiq Web settings](https://statiq.dev/web/configuration/settings)).

- `SiteTitle`: The title of the site. This should be defined regardless of whether `Logo` is because it's used for the page title, alt attributes, etc. 
- `Logo`: The logo file to use in the navigation bar (include a leading slash if providing a relative path). If not provided, the `SiteTitle` will be used.
- `EditRoot`: The root link to use for editing pages, usually set to a value like `https://github.com/org/repo/edit/develop/input` (do not use a trailing slash).

### Colors

The following settings control the color scheme of the theme. For any that are not defined, the default Bootstrap values will be used.

- `sass-blue`
- `sass-indigo`
- `sass-purple`
- `sass-pink`
- `sass-red`
- `sass-orange`
- `sass-yellow`
- `sass-green`
- `sass-teal`
- `sass-cyan`
- `sass-primary`
- `sass-secondary`
- `sass-success`
- `sass-info`
- `sass-warning`
- `sass-danger`
- `sass-light`
- `sass-dark`

Note that because these variables are injected at the top of the Bootstrap Sass definitions, and their order is undefined, they cannot use other variables in their value and must contain explicit values. If defining as a hex color code from a YAML settings file, you also need to surround the value in quotes since the `#` symbol designates a comment in YAML.

### Fonts

The following settings control the fonts in use by the theme. Note that values here should be enclosed in single quotes, for example `'Roboto Mono'`. You can specify fallback fonts by using commas, for example `'Roboto Mono', monospace`. For any that are not defined, the Roboto family of fonts will be used.

- `sass-font-family-sans-serif`
- `sass-font-family-serif`
- `sass-font-family-monospace`

To include a different set of font resources, you can change the setting `FontLink` to a Google Fonts (or other) URL for use in a `<link>` element in the header. Otherwise, you can also include any font resources you want in a `_Head.cshtml` override file in your input directory (in which case you'll likely want to set `FontLink` to an empty string to avoid including the default set of fonts in addition to yours).

## Page

These can be set in front matter, a sidecar file, etc. (in addition to any [Statiq Web settings](https://statiq.dev/web/configuration/settings)).

- `EditLink`: A more specific editing link that overrides `EditRoot` if needed.
- `ShowInNavigation`: Set to `false` to hide the page in the top navigation.
- `NavigationTitle`: The title of a page when displayed in the navigation, otherwise the normal title will be used.
- `BreadcrumbTitle`: The title of a page when displayed in the breadcrumbs, otherwise the normal title will be used.

# Partials

Replace or copy any of these Razor partials in your `input` folder to override sections of the site:

TODO

# Sections

In addition to globally changing sections of the site using the partials above you can also add the following Razor sections to any given page to override them for that page (which will typically disable the use of the corresponding partial):

TODO

# Index Page

You should provide your own `index.cshtml` or `index.md`.

# Xrefs

Xrefs for API pages are prefixed with `api-`.