#!/usr/bin/env bash
# Packs Acl and OrgChart from their clones next to this repository and copies the packages into modules-feed/,
# the NuGet folder Mapper restores them from (see nuget.config). Run after changing a module, with the module's
# version bumped in its Directory.Build.props (NuGet caches packages by version), then update the versions in
# Mapper/Borc.DataMapper.Web.csproj and commit modules-feed/.
# Usage: tools/update-modules.sh [path-to-Acl-clone] [path-to-organization-clone]
set -euo pipefail

here="$(cd "$(dirname "$0")/.." && pwd)"
acl="${1:-$here/../Acl}"
org="${2:-$here/../organization}"
feed="$here/modules-feed"

dotnet pack "$acl/Acl.sln" -c Release
dotnet pack "$org/OrgChart.sln" -c Release

mkdir -p "$feed"
rm -f "$feed"/*.nupkg
acl_version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$acl/Directory.Build.props")
org_version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$org/Directory.Build.props")
for p in Acl.Core Acl.EFCore Acl.AspNetCore Acl.Razor; do cp "$acl/artifacts/packages/$p.$acl_version.nupkg" "$feed/"; done
for p in OrgChart.Core OrgChart.EFCore OrgChart.AspNetCore OrgChart.Razor OrgChart.Acl; do cp "$org/artifacts/packages/$p.$org_version.nupkg" "$feed/"; done
ls -l "$feed"
