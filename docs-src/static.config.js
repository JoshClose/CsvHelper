import fs from "fs-extra";
import ExtractTextPlugin from "extract-text-webpack-plugin";

function createData(name) {
	return () => ({
		className: name,
		data: fs.readFileSync(`./src/pages/${name}.md`, "utf-8")
	});
}

export default {
	siteRoot: "https://joshclose.github.io",
	basePath: "/CsvHelper/",
	getRoutes: async () => {
		return [
			{
				path: "/",
				component: "src/components/content",
				getData: createData("home")
			},
			{
				path: "/reading",
				component: "src/components/content",
				getData: createData("reading")
			},
			{
				path: "/writing",
				component: "src/components/content",
				getData: createData("writing")
			},
			{
				path: "/mapping",
				component: "src/components/content",
				getData: createData("mapping")
			},
			{
				path: "/configuration",
				component: "src/components/content",
				getData: createData("configuration")
			},
			{
				path: "/type-conversion",
				component: "src/components/content",
				getData: createData("type-conversion")
			},
			{
				path: "/examples",
				component: "src/components/content",
				getData: createData("examples")
			},
			{
				path: "/change-log",
				component: "src/components/content",
				getData: createData("change-log")
			},
			{
				is404: true,
				component: "src/components/404",
			}
		]
	},
	webpack: (config, { defaultLoaders, stage }) => {
		config.plugins.push(
			new ExtractTextPlugin({
				filename: "[name].css"
			})
		);

		let loaders = []

		if (stage === 'dev') {
			loaders = [{ loader: 'style-loader' }, { loader: 'css-loader' }, { loader: 'sass-loader' }]
		} else {
			loaders = [
				{
					loader: 'css-loader',
					options: {
						importLoaders: 1,
						minimize: stage === 'prod',
						sourceMap: false,
					},
				},
				{
					loader: 'sass-loader',
					options: { includePaths: ['src/'] },
				},
			]

			// Don't extract css to file during node build process
			if (stage !== 'node') {
				loaders = ExtractTextPlugin.extract({
					fallback: {
						loader: 'style-loader',
						options: {
							sourceMap: false,
							hmr: false,
						},
					},
					use: loaders,
				})
			}
		}

		config.module.rules = [
			{
				oneOf: [
					{
						test: /\.s(a|c)ss$/,
						use: loaders,
					},
					defaultLoaders.cssLoader,
					defaultLoaders.jsLoader,
					defaultLoaders.fileLoader,
				],
			},
		]
		return config
	},
	onBuild: async () => {
		console.log("Copying build to docs folder.");
		await fs.remove("../docs");
		await fs.copy("./dist", "../docs")
	}
}