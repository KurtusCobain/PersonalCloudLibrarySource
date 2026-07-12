from pathlib import Path

path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.csproj")
text = path.read_text(encoding="utf-8-sig")
old = '    <Compile Include="Transfers\\LocalTransferAdapter.cs" />'
new = '''    <Compile Include="Transfers\\LocalTransferAdapter.cs" />
    <Compile Include="Transfers\\RcloneCommandBuilder.cs" />
    <Compile Include="Transfers\\RcloneProcessRunner.cs" />
    <Compile Include="Transfers\\RcloneProgressParser.cs" />
    <Compile Include="Transfers\\RcloneTransferAdapter.cs" />
    <Compile Include="Transfers\\RcloneTransferModels.cs" />'''
if new not in text:
    if old not in text:
        raise SystemExit("Rclone transfer project registration target missing.")
    text = text.replace(old, new, 1)
path.write_text(text, encoding="utf-8-sig")
