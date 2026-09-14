// Standard npm dependencies, with an optional external dependency tree for hosted workspaces.
const {createRequire}=require('module');
const external=process.env.BARA_NODE_MODULES;
module.exports=external?createRequire(require('path').resolve(external,'package.json')):require;
