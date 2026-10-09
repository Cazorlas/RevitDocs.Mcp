# Public repository source review

The shipped [catalog](../src/Paper.RevitDocs.Mcp/Configuration/repository-sources.json) enables reviewed MIT sources at immutable commits.
The review uses GitHub repository metadata, the license file at each commit, and representative source headers checked on 2026-10-09.
Enabled sources require an explicit `sync <source-id>` command before a repository snapshot becomes available.
Search never downloads a repository.

| Source | Default branch at review | Reviewed commit | License evidence | Decision |
| --- | --- | --- | --- | --- |
| The Building Coder Samples | `master` | `cf3748045978bc35fcae88417c3024209be44fbe` | [License.md](https://github.com/jeremytammik/the_building_coder_samples/blob/cf3748045978bc35fcae88417c3024209be44fbe/License.md) | Enabled, MIT; Jeremy Tammik attribution. |
| Nice3point Revit Toolkit | `main` | `b5abec5ca7302e3adc4e821cd3a8a9d4f9032e7e` | [LICENSE.md](https://github.com/Nice3point/RevitToolkit/blob/b5abec5ca7302e3adc4e821cd3a8a9d4f9032e7e/LICENSE.md) | Enabled, MIT; Nice3point attribution. |
| ricaun RevitTest | `master` | `950a26455a14b0608e239747f074a413a3cbfd79` | [LICENSE](https://github.com/ricaun-io/RevitTest/blob/950a26455a14b0608e239747f074a413a3cbfd79/LICENSE) | Enabled, MIT; ricaun attribution. |
| Autodesk Revit SDK Samples | `master` | `4ca6a3d6f0ae6e166b514e8324f65f057da27b64` | [Root LICENSE](https://github.com/jeremytammik/RevitSdkSamples/blob/4ca6a3d6f0ae6e166b514e8324f65f057da27b64/LICENSE), [Autodesk sample header](https://github.com/jeremytammik/RevitSdkSamples/blob/4ca6a3d6f0ae6e166b514e8324f65f057da27b64/SDK/Samples/AddSpaceAndZone/CS/Command.cs) | Disabled; source distribution rights remain unresolved. |

## Attribution and distribution

The [shipped notices](../src/Paper.RevitDocs.Mcp/THIRD-PARTY-NOTICES.md) contain the reviewed MIT copyright and permission notices.
Copies or substantial portions of reviewed source require these notices and any existing file copyright notices.
[Building Coder source headers](https://github.com/jeremytammik/the_building_coder_samples/blob/cf3748045978bc35fcae88417c3024209be44fbe/BuildingCoder/CmdAnalyticalModelGeom.cs) also name Autodesk Inc.; those notices remain part of the source.
The inspected `CmdAnalyticalModelGeom.cs` header contains a copyright notice, but no separate license terms or object-code-only distribution clause.
The pinned repository's MIT license supplies the distribution grant for that file; the copyright notice remains intact.
This differs from the inspected Autodesk SDK sample, whose header includes its own object-code-only distribution grant.
Source content retains its upstream license and attribution independently of this project's MIT License.

The Autodesk SDK root license identifies Jeremy Tammik and MIT, but the inspected Autodesk-owned sample grants distribution in object code form.
The server returns source text.
The root license alone does not resolve that mismatch; the SDK entry retains a null reviewed license and remains disabled pending clarification of source distribution rights.

## Revision policy

The catalog pins reviewed commits instead of tracking mutable branch heads.
A revision update requires a fresh license and attribution review plus an update to the catalog regression tests.
The reviewed default branches differ from the previous catalog branch values for all enabled sources.
Pinned commits remove dependence on those branch names during synchronization.
