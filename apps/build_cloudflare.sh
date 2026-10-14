#!/usr/bin/env bash
set -euo pipefail

# Run from the repository root, even when invoked from another directory.
cd "$(dirname "${BASH_SOURCE[0]}")/.."
repo_root="$PWD"
browser_dir="$repo_root/apps/Chromaticity.Tools.Browser"
browser_project="$browser_dir/Chromaticity.Tools.Browser.csproj"
publish_dir="$repo_root/artifacts/cloudflare-publish"
sdk_dir="$repo_root/artifacts/cloudflare-dotnet"
mkdir -p "$sdk_dir"

# Pages does not provide the .NET SDK and browser workload for this project.
curl -fsSL --retry 3 https://dot.net/v1/dotnet-install.sh -o "$sdk_dir/dotnet-install.sh"
bash "$sdk_dir/dotnet-install.sh" --channel 10.0 --install-dir "$sdk_dir"
export DOTNET_ROOT="$sdk_dir"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

dotnet --version
dotnet workload install wasm-tools

# Clean the current project-reference graph, including the libraries under src/.
dotnet clean "$browser_project" -c Release -p:GeneratePackageOnBuild=false
# An SDK/workload change can leave native WASM tables and published assets that
# dotnet clean cannot find in the current SDK's intermediate file list.
rm -rf "$browser_dir/bin" "$browser_dir/obj" "$publish_dir"

dotnet restore "$browser_project" -p:GeneratePackageOnBuild=false
dotnet publish "$browser_project" \
  -c Release --no-restore -p:GeneratePackageOnBuild=false --output "$publish_dir"

# Cache policy regressions must stop the deployment before staging the site.
python3 -m unittest discover -s apps -p test_prepare_site.py

# Only replace the generated site; prepare_site.py requires a fresh directory.
rm -rf "$repo_root/artifacts/site"
python3 apps/prepare_site.py --publish-dir "$publish_dir"
