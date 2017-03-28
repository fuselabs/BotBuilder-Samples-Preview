@echo off
if exist Dialogs\RealEstate.json goto generate
..\core\Search.Tools.Extract\bin\%1\netcoreapp1.0\win10-x64\Search.Tools.Extract.Exe realestate listings 93B04DA93FF693841A35B66AF9D32023 -g Dialogs\RealEstateBot-histogram.bin -v price -h Dialogs\RealEstateBot-histogram.bin -o Dialogs\RealEstate.json 

:generate
..\core\Search.Tools.Generate\bin\%1\netcoreapp1.0\win10-x64\Search.Tools.Generate.exe Dialogs\RealEstate.json -t ..\core\Search.Tools.Generate\SearchTemplate.json -o Dialogs\RealEstateModel.json 
echo add -u
