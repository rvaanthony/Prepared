const path = require('path');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');
const { CleanWebpackPlugin } = require('clean-webpack-plugin');
const CopyWebpackPlugin = require('copy-webpack-plugin');

module.exports = (env, argv) => {
  const isProduction = argv.mode === 'production';

  return {
    entry: {
      layout: './src/layout.js',
      calls: './src/calls.js',
      // CSS entries
      siteStyles: './wwwroot/css/site.css'
    },
    output: {
      path: path.resolve(__dirname, 'wwwroot/dist'),
      filename: '[name].bundle.js',
      clean: true,
      publicPath: '/dist/'
    },
    module: {
      rules: [
        {
          test: /\.js$/,
          exclude: /node_modules/,
          use: {
            loader: 'babel-loader',
            options: {
              presets: ['@babel/preset-env']
            }
          }
        },
        {
          test: /\.css$/,
          use: [
            isProduction ? MiniCssExtractPlugin.loader : 'style-loader',
            'css-loader'
          ]
        },
        {
          test: /\.(png|jpg|jpeg|gif|svg|ico)$/i,
          type: 'asset/resource',
          generator: {
            filename: 'images/[name].[hash][ext]'
          }
        },
        {
          test: /\.(woff|woff2|eot|ttf|otf)$/i,
          type: 'asset/resource',
          generator: {
            filename: 'fonts/[name].[hash][ext]'
          }
        }
      ]
    },
    plugins: [
      new CleanWebpackPlugin({
        cleanOnceBeforeBuildPatterns: ['**/*', '!**/.gitkeep']
      }),
      new MiniCssExtractPlugin({
        filename: '[name].bundle.css'
      }),
      new CopyWebpackPlugin({
        patterns: [
          {
            from: path.resolve(__dirname, 'wwwroot/img'),
            to: path.resolve(__dirname, 'wwwroot/dist/images'),
            noErrorOnMissing: true
          }
        ]
      })
    ],
    resolve: {
      extensions: ['.js', '.json'],
      alias: {
        'jquery': 'jquery/dist/jquery.min.js'
      }
    },
    optimization: {
      splitChunks: {
        chunks: 'all',
        cacheGroups: {
          vendor: {
            test: /[\\/]node_modules[\\/]/,
            name: 'vendors',
            priority: 10,
            reuseExistingChunk: true
          }
        }
      }
    },
    devtool: isProduction ? 'source-map' : 'eval-source-map',
    devServer: {
      static: {
        directory: path.join(__dirname, 'wwwroot'),
        publicPath: '/'
      },
      port: 5173,
      hot: true,
      open: false,
      compress: true
    }
  };
};

