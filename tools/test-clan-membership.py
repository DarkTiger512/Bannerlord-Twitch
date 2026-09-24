"""Test the production load patch against installed Bannerlord roster methods.

Creates isolated managed objects, never opens saves or starts a campaign.
Usage: python tools/test-clan-membership.py OUTPUT_DIR [GAME_DIR]
"""
from pathlib import Path
import os
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
out = Path(sys.argv[1]).resolve()
game = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(r'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord')
bin_dir = game / 'bin/Win64_Shipping_Client'
harmony = next((game / f'Modules/{module}/bin/Win64_Shipping_Client/0Harmony.dll'
                for module in ['TAOM.Dependencies', 'Bannerlord.Harmony', 'TAOM']
                if (game / f'Modules/{module}/bin/Win64_Shipping_Client/0Harmony.dll').exists()))
out.mkdir(parents=True, exist_ok=True)
compiler = Path(os.environ['WINDIR']) / 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
netstandard = Path(os.environ['ProgramFiles(x86)']) / 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8/Facades/netstandard.dll'
refs = ['TaleWorlds.CampaignSystem', 'TaleWorlds.Core', 'TaleWorlds.Library',
        'TaleWorlds.ObjectSystem', 'TaleWorlds.Localization']
exe = out / 'ClanMembershipTests.exe'
subprocess.run([str(compiler), '/nologo', '/target:exe', f'/out:{exe}',
                *[f'/reference:{bin_dir / (ref + ".dll")}' for ref in refs],
                f'/reference:{harmony}',
                f'/reference:{netstandard}',
                str(root / 'tools/clan-membership-fixture.cs'),
                str(root / 'BannerlordTwitch/BLTAdoptAHero/Util/ViewerClanMembershipPatch.cs')], check=True)
subprocess.run([str(exe), str(bin_dir), str(harmony)], check=True)
