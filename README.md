# Org.Grush.HomeBase


## Setup

#### Installing .NET if needed
_TODO: is this not just in a package repo?_

```shell
if [[ `cat /etc/debian_version` == "13."* ]]; then
  # as instructed by .NET docs
  wget https://packages.microsoft.com/config/debian/13/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
  sudo dpkg -i packages-microsoft-prod.deb
  rm packages-microsoft-prod.deb

  # install the .NET 10 SDK and AOT dependencies
  sudo apt-get update && \
    sudo apt-get install -y dotnet-sdk-10.0 clang zlib1g-dev
fi

mkdir ~/bin
```

#### Check out the source

```shell
git clone https://github.com/skgrush/Org.Grush.HomeBase.git
cd Org.Grush.HomeBase/
```

#### Build and link the source
```shell


dotnet publish Org.Grush.HomeBase.WeatherStationCli
ln --symbolic \
  $(realpath ./Org.Grush.HomeBase.WeatherStationCli/bin/Release/net10.0/linux-arm64/publish/Org.Grush.HomeBase.WeatherStationCli) \
  ~/bin/Org.Grush.HomeBase.WeatherStationCli
```
