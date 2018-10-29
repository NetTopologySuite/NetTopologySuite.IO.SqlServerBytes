#!/bin/bash
set -e
dotnet build -c Release
dotnet test NetTopologySuite.IO.SqlServerBytes.Test --no-build --no-restore -c Release
if [ "$TRAVIS_EVENT_TYPE" = "push" ] && [ "$TRAVIS_BRANCH" = "master" ]
then
    dotnet pack --no-build --no-dependencies --version-suffix=travis$(printf "%05d" $TRAVIS_BUILD_NUMBER) --output $TRAVIS_BUILD_DIR -c Release
    dotnet nuget push *.nupkg -k $MYGET_API_KEY -s https://www.myget.org/F/nettopologysuite/api/v2/package
fi
