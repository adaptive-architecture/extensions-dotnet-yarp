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
  --nologo \
  --no-build \
  --filter \"FullyQualifiedName!~AdaptArch.Extensions.Yarp.Samples\" \
  -p:CollectCoverage=\"true\" \
  -p:CoverletOutputFormat=\"json,lcov,opencover\"  \
  -p:CoverletOutput=\"../../coverage/\" \
  -p:MergeWith=\"../../coverage/coverage.json\"


#  -p:Threshold=80 \
#  -p:ThresholdStat=total \
#  --logger "console;verbosity=normal" \
