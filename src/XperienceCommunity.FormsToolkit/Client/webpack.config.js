const webpackMerge = require("webpack-merge");
const baseWebpackConfig = require("@kentico/xperience-webpack-config");

module.exports = (opts, argv) => {
  const baseConfig = (webpackConfigEnv, buildArguments) => baseWebpackConfig({
    orgName: "xperience-community",
    projectName: "forms-toolkit",
    webpackConfigEnv,
    argv: buildArguments,
  });

  return webpackMerge.merge({
    module: {
      rules: [{
        test: /\.(js|ts)x?$/,
        exclude: [/node_modules/],
        loader: "babel-loader",
      }],
    },
    output: { clean: true },
    devServer: { port: 3019 },
  }, baseConfig(opts, argv));
};
