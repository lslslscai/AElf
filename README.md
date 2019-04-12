# AElf - A Decentralized Cloud Computing Blockchain Network 

[![Build Status](https://travis-ci.org/AElfProject/AElf.svg?branch=dev)](https://travis-ci.org/AElfProject/AElf)
[![Build status](https://ci.appveyor.com/api/projects/status/wnehtmk2up4l5w5j/branch/dev?svg=true)](https://ci.appveyor.com/project/AElfProject/aelf/branch/dev)
[![GitHub closed issues](https://img.shields.io/github/issues-closed/aelfproject/aelf.svg)](https://app.gitkraken.com/glo/board/XKsOZJarBgAPseno)
[![codecov](https://codecov.io/gh/AElfProject/AElf/branch/dev/graph/badge.svg)](https://codecov.io/gh/AElfProject/AElf)
[![lisence](https://img.shields.io/github/license/AElfProject/AElf.svg)](https://github.com/AElfProject/AElf/blob/dev/LICENSE)
[![Nuget](https://img.shields.io/nuget/v/AElf.OS.svg)](https://www.nuget.org/packages?q=aelf)
[![MyGet (with prereleases)](https://img.shields.io/myget/aelf-project-dev/vpre/aelf.os.svg?label=myget)](https://www.myget.org/gallery/aelf-project-dev)

[![Twitter Follow](https://img.shields.io/twitter/follow/aelfblockchain.svg?label=%40aelfblockchain&style=social)](https://twitter.com/aelfblockchain)
[![Gitter](https://badges.gitter.im/aelfproject/community.svg)](https://gitter.im/aelfproject/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)

Welcome to AElf's official GitHub repository ! 

AElf is a blockchain system aiming to achieve scalability and extensibility through the use of side-chains and flexible design. To support multiple use cases AElf makes it as easy as possible to extend/customize the system by providing easy to use tools and frameworks in order to customize the chains and write smart contracts. AElf will eventually support various languages that will let developers choose the one they are the most comfortable with.

For more information you can follow these links:
* [Official website](https://aelf.io)
* [White Paper](https://grid.hoopox.com/aelf_whitepaper_EN.pdf?v=1) 

## Getting Started

### This repository

This repo contains mainly two things: the code for an AElf node and the command line tool used to interact with it.

### Prerequisites

Because most of AElf is written in C# with the dotnet core framework, you will need to download and install the [.NET Core SDK](https://dotnet.microsoft.com/download).

Concerning the IDE, you can use any as long as it has support for C#. Good choices include Jetbrains Rider and Visual Studio.

Finally if you want to run the node you will have to install and run a Redis or SSDB for the nodes data.

### Build and run

### Run the tests

Our tests are in the **test** folder, in order to run you only have to execute the following script:

```bash
sh scripts/test.sh
```

## Contributing

If you have a reasonable understanding of blockchain technology and at least some notions of C# you can of course contribute. We also appreciate other types of contributions such as documentation improvements or even correcting typos in the code if you spot any.

We expect every contributor to be respectful and constructive so that everyone has a positive experience, you can find out more in our [code of conduct](https://github.com/AElfProject/AElf/blob/dev/CODE_OF_CONDUCT.md).

### Reporting an issue

We currently only use GitHub for tracking issues, feature request and pull requests. If you're not familiar with these tools have a look at the [GitHub](https://help.github.com/en) documentation.

#### Bug report

If you think you have found a bug in our system feel free to open a GitHub issue, but first:
- check with GitHubs search engine that the bug doesn't already exist
- in the request give as much information as possible such as: the OS, the version of AElf, how to reproduce...

#### Missing feature

We also use the GitHub issue tracker for features. If you think that some piece of functionality is missing in AElf, you can open an issue with the following in mind:
- check for similare feature requests already open.
- provide as much detail and context as possible
- be as convincing as possible as to why we need this feature and how everybody can benefit from it.

### Pull request

For any non trivial modification to the code, the pull requests should be associated with an issue that was previously discussed. During the time you implement and a not ready for review prefix the PR with ```[WIP]```. Don't forget to do the following:
- add a description in the pull request saying which issue you are fixing/implementing. Be as explicit as possible about the changes.
- add the tests corresponding to your modifications.

When the you are ready for a review by the core team, just remove ```[WIP]``` from your PRs title and others will review, either this will lead to discussion or to the code being merged. 

## Versioning

We use Semantic Versioning (SemVer) for versioning, if you're intereted in closely following AElf's developement please check out the [SemVer docs](https://semver.org/).

## License

AElf is licenced under [MIT](https://github.com/AElfProject/AElf/blob/dev/LICENSE)