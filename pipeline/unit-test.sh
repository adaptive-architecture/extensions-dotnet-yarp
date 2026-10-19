#!/bin/bash

# in case the CI environment varialbe has a non-empty value ignore the "WindowOnly" tests
if [ -n "$CI" ]; then
  echo "Disabling TESTCONTAINERS RUYK"
  export TESTCONTAINERS_RYUK_DISABLED=true
else
  # Build first to avoid file locking issues during parallel test execution
  dotnet clean --nologo
  dotnet build --nologo --no-incremental
fi

rm -rf ./coverage/*
rm -rf ./test/TestResults

dotnet test \
  --no-build \
  --coverlet \
  --coverlet-include "[AdaptArch.*]*" \
  --coverlet-exclude "[*Tests]*" \
  --coverlet-exclude "[AdaptArch.Extensions.Yarp.Samples.*]*" \
  --coverlet-output-format json \
  --coverlet-output-format lcov \
  --coverlet-output-format opencover \
  --results-directory ./coverage
