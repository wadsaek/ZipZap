# מבוא

אחסון קבצים יכול להיות נושא בעיתי ממספר סיבות.
1. חוסר מקום אחסון פיזי על המחשב
2. פתרונות ענן דורשות דרכים מוזרות כדי לגשת לקבצים שלך מתוך אפליקציות, או הורדת הקבצים, שממנה רצינו להיפטר

# רעיון

ניתן להיפטר מהבעיה דרך מערכות קבצים ווירטואליות במערכות הפעלה Unix-like. הן יכולות לתת גישה לקבצים בדיוק כאילו הם קבצים רגילים על הכונן שלך. מערכת הקבצים הווירטואלית שעניינה אותי היא [sshfs](https://github.com/libfuse/sshfs).

בפרויקט הזה אני אפתח שירות רשת שתשמור קבצים ולקוח לשירות זו שתשמש כשרת [ssh](https://www.rfc-editor.org/search/rfc_search_detail.php?title=the+secure+shell&pubstatus%5B%5D=Any&pub_date_type=any)/[sftp (v6)](https://www.sftp.net/specification) וכשרת https שיוכל לתת גישה לקבצים אלו דרך אתר אינטרנט.

# טבלאות
- Files
  - fileId: uuid (primary key)
  - physicalLocation: string
  - name: string
  - virtualLocationId: uuid (foreign key to `Directories`)
  - permissions: short int (2 bytes)
  - fileOwner: int
  - fileGroup: int

- Directories
  - dirId: uuid (primary key)
  - ownerId (foreign key to `Users`)
  - name: string
  - virtualLocationId: uuid | null (foreign key to `Directories`)
  - permissions: short int (2 bytes)
  - dirOwner: int
  - dirGroup: int

- Users
  - id: uuid (primary key)
  - username: string
  - passwordHash: byte array of length 64
  - email

- SshKeys
  - key: string
  - userId: uuid (foreign key to `Users`)

- FileAccess
  - fileAccessId: uuid (primary key)
  - fileId: uuid (foreign key to `Files`)
  - targetUserId (foreign key to `Users`)
  - expiratoinDate: DateTime | null
