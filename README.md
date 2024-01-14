# DD2470_Clustered_Volume_Renderer

![Temple surrounded by many point lights in fog.](img/Volumetric4.PNG)

This is my project for the [KTH](https://kth.se) course DD2470 Advanced Topics in Visualization and Computer Graphics. My goal for the project was a performance comparison between forward shading and clustered forward shading for volumetric fog. Mostly an excuse for me to implement and learn clustered shading and volumetric fog rendering.

It implements volumetric fog in a similar way as what is described by Bart Wronski in his [SIGGRAPH 2014 presentation](https://bartwronski.files.wordpress.com/2014/08/bwronski_volumetric_fog_siggraph2014.pdf) as well as using ideas from Sebastien Hillaires [SIGGRAPH 2015 presenation](https://www.ea.com/frostbite/news/physically-based-unified-volumetric-rendering-in-frostbite) on the same topic.

There are a few flickering issues as I never got the temporal jittering + anti-aliasing to be completely stable.

> [!WARNING]  
> This repo contains submodules that are required for the project to build, so make sure you initialize the submodules or clone recursively!

# Assets

The assets that are a part of this repo is uploaded to GitHubs git lfs service which has a bandwidth limit so downloading them will likely not work. I'm working on an alternative way to download the files.

# Gallery

![Three temples on a hill side fading into height fog.](img/Volumetric.PNG)
![Three temples lit in red, green, and blue showing a volumetric glow.](img/Volumetric3.PNG)
![Temple surrounded by many point lights in fog.](img/Volumetric4.PNG)