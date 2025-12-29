#!/bin/bash

# Default values
configuration=""
version=""
nuget_api_key=""
github_api_key=""

# Parse command-line arguments
while getopts "c:v:n:g" opt; do
  case $opt in
    c) configuration="$OPTARG" ;;
    v) version="$OPTARG" ;;
    n) nuget_api_key="$OPTARG" ;;
    g) github_api_key="$OPTARG" ;;
  esac
done

# Check if any required argument is missing
if [ -z "$configuration" ] || [ -z "$version" ] || [ -z "$nuget_api_key" ]; then
  echo "Usage: $0 -c config -v version -n nuget_api_key -g github_api_key"
  exit 1
fi

projects=( \
  "Auth" \
  "OpenApi" \
)

rm -rf ./.nuget/*.nupkg
rm -rf ./.nuget/*.snupkg

# Loop over the array and print each variable
for project in "${projects[@]}"; do
  echo "Publishing $project"

  dotnet build ./src/$project/$project.csproj --configuration $configuration \
    -p:ContinuousIntegrationBuild=true -p:CI_BUILD=true -p:Version=$version

  dotnet pack ./src/$project/$project.csproj --configuration $configuration -p:Version=$version \
    -p:CI_BUILD=true

  # dotnet nuget push ./src/$project/bin/$configuration/*.nupkg \
  #   --api-key $github_api_key \
  #   --source https://nuget.pkg.github.com/adaptive-architecture/index.json \
  #   --skip-duplicate

  dotnet nuget push ./src/$project/bin/$configuration/*.nupkg \
    --api-key $nuget_api_key \
    --source https://api.nuget.org/v3/index.json \
    --skip-duplicate

  cp ./src/$project/bin/$configuration/*.nupkg ./.nuget/

  sed -i "s/Include=\"AdaptArch\.Extensions\.Yarp\.$project\".*Version=\".*\"/Include=\"AdaptArch.Extensions.Yarp.$project\" Version=\"$version\"/" ./Directory.Packages.props
done
