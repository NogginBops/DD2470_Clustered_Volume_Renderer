# Use this when using the unmodified version of bc7enc.exe
# Get-ChildItem -File ..\textures_png\ -Include "*.png" -Exclude "*_ddn.png" -Recurse | Foreach { ..\..\..\Tools\bc7enc.exe -s -y $($_) }
# Get-ChildItem -File ..\textures_png\ -Include "*_ddn.png" -Recurse | Foreach { ..\..\..\..\Tools\bc7enc.exe -5 -y $($_) }
Get-ChildItem -File ..\textures_png\ -Include "*.png" -Exclude "*_ddn.png" -Recurse | Foreach { ..\..\..\..\Tools\bc7enc.exe -g -s -y -mip -mP $($_) }
Get-ChildItem -File ..\textures_png\ -Include "*_ddn.png" -Recurse | Foreach { ..\..\..\..\Tools\bc7enc.exe -g -5 -y -mip -mN $($_) }