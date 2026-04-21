import fetch from 'node-fetch';
import chalk from 'chalk';
import fs from 'node:fs/promises';
import path from 'node:path';
import { createClient, defaultPlugins } from '@hey-api/openapi-ts';

const desiredPathPrefix = '/umbraco/management/api/v1/advanced-permissions';

console.log(chalk.green('Generating OpenAPI client...'));

const swaggerUrl = process.argv[2];
if (swaggerUrl === undefined) {
    console.error(chalk.red('ERROR: Missing URL to OpenAPI spec'));
    console.error(`Please provide the URL to the OpenAPI spec as the first argument found in ${chalk.yellow('package.json')}`);
    console.error(`Example: node generate-openapi.js ${chalk.yellow('http://localhost:5266/umbraco/swagger/management/swagger.json')}`);
    process.exit(1);
}

// Ignore self-signed certificates if the TestSite is served over HTTPS on localhost
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

console.log('Ensure your Umbraco instance is running');
console.log(`Fetching OpenAPI definition from ${chalk.yellow(swaggerUrl)}`);

fetch(swaggerUrl)
    .then(async (response) => {
        if (!response.ok) {
            console.error(chalk.red(`ERROR: OpenAPI spec returned with a non OK (200) response: ${response.status} ${response.statusText}`));
            console.error('The URL to your Umbraco instance may be wrong or the instance is not running');
            console.error(`Please verify or change the URL in the ${chalk.yellow('package.json')} for the script ${chalk.yellow('generate-client')}`);
            process.exit(1);
        }

        const openApiResponse = await response.json();
        const filteredSpec = filterOpenApiSpec(openApiResponse);

        console.log('OpenAPI spec fetched successfully');
        console.log(`Calling ${chalk.yellow('hey-api')} to generate TypeScript client`);

        await createClient({
            input: filteredSpec,
            output: 'src/api/generated',
            plugins: [
                ...defaultPlugins,
                '@hey-api/client-fetch',
                { name: '@hey-api/typescript', enums: 'typescript' },
                { name: '@hey-api/sdk', asClass: true, classNameBuilder: '{{name}}Service' },
            ],
        });

        await addTsNoCheckToRuntimeFiles('src/api/generated');
        console.log(chalk.green('Client generated at src/api'));
    })
    .catch((error) => {
        console.error(`ERROR: Failed to connect to the OpenAPI spec: ${chalk.red(error.message)}`);
        console.error('The URL to your Umbraco instance may be wrong or the instance is not running');
        console.error(`Please verify or change the URL in the ${chalk.yellow('package.json')} for the script ${chalk.yellow('generate-client')}`);
        process.exit(1);
    });

/**
 * hey-api's generated runtime files (client/* and core/*) trip TypeScript's
 * exactOptionalPropertyTypes. They are vendor code we don't own, so we prepend
 * `// @ts-nocheck` after generation to keep strict type-checking for our own
 * code without weakening the project's tsconfig.
 * @param {string} outputDir
 */
async function addTsNoCheckToRuntimeFiles(outputDir) {
    const runtimeDirs = ['client', 'core'];
    for (const subdir of runtimeDirs) {
        const dir = path.join(outputDir, subdir);
        const files = await fs.readdir(dir).catch(() => []);
        for (const file of files) {
            if (!file.endsWith('.ts')) continue;
            const fullPath = path.join(dir, file);
            const contents = await fs.readFile(fullPath, 'utf8');
            if (contents.startsWith('// @ts-nocheck')) continue;
            await fs.writeFile(fullPath, `// @ts-nocheck\n${contents}`, 'utf8');
        }
    }
}

function filterOpenApiSpec(openApiSpec) {
    const filteredPaths = Object.keys(openApiSpec.paths)
        .filter((path) => path.startsWith(desiredPathPrefix))
        .reduce((obj, key) => {
            obj[key] = openApiSpec.paths[key];
            return obj;
        }, {});

    return {
        ...openApiSpec,
        paths: filteredPaths,
    };
}
