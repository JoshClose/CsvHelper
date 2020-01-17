import React from "react";
import path from "path"
import fs from "fs";
import toc from "./src/data/toc.json";

function createData(name) {
	const className = name.substring(name.lastIndexOf("/"));
	const tocKey = name.substring(0, name.indexOf("/"));

	return () => ({
		className: className,
		data: fs.readFileSync(`./src/content/${name}.md`, "utf-8"),
		toc: tocKey ? toc[tocKey] : toc[name]
	});
}

function createRoutesFromToc() {
	const routes = [];
	Object.entries(toc).forEach(([key, value]) => {
		createRoutesFromTocItem(value, routes);
	});

	return routes;
}

function createRoutesFromTocItem(item, routes) {
	if (!item.path.includes("#")) {
		const route = {
			path: `${item.path}`,
			template: "src/pages/documentation",
			getData: createData(item.path)
		};
		routes.push(route);
	}

	if (item.children) {
		item.children.forEach(child => {
			createRoutesFromTocItem(child, routes);
		});
	}
}

export default {
	siteRoot: "https://joshclose.github.io",
	stagingBasePath: "CsvHelper",
	basePath: "CsvHelper",
	Document: ({ Html, Head, Body, children, state: { siteData, renderMeta } }) => (
		<Html lang="en-US">
			<Head>
				<meta charSet="UTF-8" />
				<meta name="viewport" content="width=device-width, initial-scale=1" />
				<link rel="apple-touch-icon" sizes="57x57" href="/apple-icon-57x57.png" />
				<link rel="apple-touch-icon" sizes="60x60" href="/apple-icon-60x60.png" />
				<link rel="apple-touch-icon" sizes="72x72" href="/apple-icon-72x72.png" />
				<link rel="apple-touch-icon" sizes="76x76" href="/apple-icon-76x76.png" />
				<link rel="apple-touch-icon" sizes="114x114" href="/apple-icon-114x114.png" />
				<link rel="apple-touch-icon" sizes="120x120" href="/apple-icon-120x120.png" />
				<link rel="apple-touch-icon" sizes="144x144" href="/apple-icon-144x144.png" />
				<link rel="apple-touch-icon" sizes="152x152" href="/apple-icon-152x152.png" />
				<link rel="apple-touch-icon" sizes="180x180" href="/apple-icon-180x180.png" />
				<link rel="icon" type="image/png" sizes="192x192" href="/android-icon-192x192.png" />
				<link rel="icon" type="image/png" sizes="32x32" href="/favicon-32x32.png" />
				<link rel="icon" type="image/png" sizes="96x96" href="/favicon-96x96.png" />
				<link rel="icon" type="image/png" sizes="16x16" href="/favicon-16x16.png" />
				<link rel="manifest" href="/manifest.json" />
				<meta name="msapplication-TileColor" content="#ffffff" />
				<meta name="msapplication-TileImage" content="/ms-icon-144x144.png" />
				<meta name="theme-color" content="#ffffff" />
				<title>CsvHelper</title>
			</Head>
			<Body>{children}</Body>
		</Html>
	),
	getRoutes: () => {
		const routes = [
			...createRoutesFromToc()
		];

		return routes;
	},
	getSiteData: ({ stage, config }) => {
		return ({
			basePath: config.basePath ? `/${config.basePath}` : "",
			isDev: stage === "dev"
		});
	},
	plugins: [
		[
			require.resolve("react-static-plugin-source-filesystem"),
			{
				location: path.resolve("./src/pages"),
			},
		],
		[
			require.resolve("react-static-plugin-react-router"),
			{
				RouterProps: {
					basename: process.env.NODE_ENV === "production" ? "/CsvHelper" : ""
				}
			}
		],
		require.resolve("react-static-plugin-sitemap"),
		require.resolve("react-static-plugin-sass")
	],
	devServer: {
		historyApiFallback: {
			disableDotRule: true
		}
	}
}
