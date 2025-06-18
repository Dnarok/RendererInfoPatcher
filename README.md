# RendererInfoPatcher

RendererInfo, like many other things, was updated to use `AssetReference` with 1.3.9. Naturally, it only had the field added - almost all of the locations where the field it obsolesced are used were untouched. This fixes that in the following locations:

* `ItemDisplay.RefresherRenderers`
* `CharacterModel.UpdateMaterials`
* `RandomizeSplatBias.Setup`
* `RendererInfo.Equals` (seriously! they didn't even implement the new field into Equals!)

Report errors to Dnarok on Discord, or on the linked Github.