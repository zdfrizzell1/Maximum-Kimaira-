<img width="445" height="409" alt="Icon1v2" src="https://github.com/user-attachments/assets/8ddae676-467d-45a7-9699-9764d875019c" />

Hello all!
I have been very much anticipating the release FF7 Revelation, so I wanted to relive the past of the OG FF7. 
And while 7th Heaven and it's modders have done a tremendous job breathing new life-stream into the game. 
I felt it still could use some polish, starting with an editor that could natively handle FF7 models, andle high poly count models and textures and have similar model functions
I could not find it, so I made one!

To install:
1- Install .NET Desktop Runtime 8.0.30 or newer: https://dotnet.microsoft.com/en-us/download/dotnet/8.0
2- Download all files to a folder and run the exe

Features:
1- Natively handle FF7 field and battle models
2- Handle high poly count models and textures (No smaller memeory limits)
3- Have similar use to the well made Kimera
4- Can move and scale entire skeletons with their P files

Known issues:
1- Battle weapons do not perfectly attach or always load textures (Could use pointers there, it's very unclear to me)
2- Battle animations do not play through well yet
3- Some textures are hard coded to use the corresponding DDS file, if one is not found it may not load
4- Modifying individual P files within a skeleton (HRC or aa file) by nature affects the underlying animation that moves it, use XYZ move entire model/scale whole model to preserve animations


This is still actively being developed so sit tight as I try to add some features, but I am by no means a great coder with C. If you do decide to use this, please credit me
Be the one to fight further modders!

<img width="1408" height="768" alt="Icon2" src="https://github.com/user-attachments/assets/8fa8ecf7-d82b-487e-8ad0-7fda103e362d" />
