# Deployment examples

These examples are organization-specific and configure:

- `Allowed Entities = Users`
- `Automatic Add Allowed = lsadmin`
- a 15-minute administrator-rights timeout
- removal at logout
- Windows Hello with password fallback (`Authentication Mode = 3`)
- remote requests

Review these values before using the scripts in another environment. Generated
MSI and `.intunewin` files are release artifacts and are intentionally excluded
from source control.

- `Intune` contains install, uninstall, and custom-detection scripts.
- `PowerShell` contains an HTTPS download-and-install example. Set its release URL
  after the MSI is uploaded; its SHA-256 is pinned to the final 2.4.2 artifact.
