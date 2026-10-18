# 12. Glossary

| Term | Definition |
|---|---|
| **Account alias** | Name chosen by the operator for an Apple ID (e.g. "Account 1"). Used in UI, folder names and telemetry instead of the Apple ID. |
| **ADP (Advanced Data Protection)** | Apple's optional end-to-end encryption for iCloud data. Must be **off** for the web API to access photos. |
| **Archive root** | Absolute path of the folder that holds all accounts' archives. |
| **Archive storage** | The file system location of the archive root: a local drive, an external drive or a network share (SMB/NFS). The app treats all of them the same. |
| **Asset** | One item in the iCloud Photos library (a photo, video or Live Photo). Can consist of several resources. |
| **Capture date** | Date the photo/video was taken (from EXIF/metadata). |
| **Added date** | Date the asset was added to iCloud. Fallback when there is no capture date. |
| **CloudKit** | Apple's backend database service; iCloud Photos is stored in CloudKit zones accessed through a web service. |
| **Cutoff date** | Today minus the retention period. Assets with an effective date before it are archived and become deletable. |
| **Deletion gate** | Rule that an asset may be deleted only if all its resources are verified, the sidecar exists and the files still match their hashes. |
| **Deletion plan** | List of assets currently passing the deletion gate, with count and size, shown before a deletion run. |
| **Dry run** | Deletion run that reports what would be deleted without calling iCloud. |
| **Edited version** | Rendered result of edits made in Photos. Archived in addition to the original. |
| **Effective date** | Capture date, otherwise added date. Used for cutoff, ordering and folder names. |
| **Journal** | Append-only JSON Lines file per account recording every state change of every asset. Single source of truth. |
| **Live Photo** | A still image plus a short video, stored as two resources. |
| **Master password** | Password that unlocks the encrypted key ring at app start. Not an Apple password. |
| **Needs attention** | State of an asset that failed 3 times; shown in the UI for manual action. |
| **Onion Architecture** | Layered architecture where dependencies point inwards towards the domain. |
| **Original** | Byte-exact file as stored in iCloud (HEIC, JPEG, DNG, MOV, …). |
| **Recently Deleted** | iCloud album holding deleted items for 30 days; they still use storage during that time. |
| **Resource** | One downloadable file of an asset: original, Live Photo video, or edited version. |
| **Retention period** | Number of months kept in iCloud (global default, per-account override). |
| **Session / trust token** | Credentials returned by Apple after sign-in/2FA that allow API access without a new 2FA for a period of time. |
| **Shared storage plan** | One iCloud+ plan whose storage is used by several Apple IDs (Apple's Family Sharing). Supported but not required; accounts may also have separate plans. |
| **Sidecar (XMP)** | `.xmp` file next to the original holding metadata that would be lost with deletion (albums, favorite, caption, …). |
| **Snapshot** | Compacted state of a journal at a sequence number, to speed up loading. |
| **SRP-6a** | Secure Remote Password protocol used by Apple sign-in; the password is never sent. |
| **Verified** | Asset state: all resources downloaded and passed size, read-back hash and decode checks. |
| **Viewable copy** | JPEG or H.264 MP4 created from an original for easy viewing on any PC. |
