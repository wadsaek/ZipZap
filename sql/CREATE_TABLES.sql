
CREATE TABLE IF NOT EXISTS migrations (
    migration_name character varying(64) NOT NULL PRIMARY KEY
);

START TRANSACTION;

DO $MIGRATION$
BEGIN
    IF NOT EXISTS (SELECT * FROM migrations WHERE migration_name = 'init') THEN
        INSERT INTO migrations VALUES ('init');

        CREATE TYPE fso_type AS ENUM ('directory', 'regular_file', 'symlink');

        CREATE TABLE IF NOT EXISTS fsos (
            id uuid NOT NULL DEFAULT (gen_random_uuid()) PRIMARY KEY,
            fso_name character varying(64) NOT NULL,
            virtual_location_id uuid NULL REFERENCES fsos (id) ON DELETE CASCADE,
            permissions bit(12) NULL,
            fso_owner character varying(64) NOT NULL,
            fso_group character varying(64) NOT NULL,

            fso_type fso_type NOT NULL DEFAULT 'regular_file',
            link_ref character varying(256) NULL,
            file_physical_path character varying(256) NULL,

            CONSTRAINT hierarchy_link CHECK(fso_type <> 'symlink' OR link_ref IS NOT NULL),
            CONSTRAINT hierarchy_file CHECK(fso_type <> 'regular_file' OR (file_physical_path IS NOT NULL AND permissions IS NOT NULL)),
            CONSTRAINT hierarchy_directory CHECK(fso_type <> 'directory' OR permissions IS NOT NULL),

            CONSTRAINT unique_fso UNIQUE (fso_name, virtual_location_id)
        );

        CREATE TABLE IF NOT EXISTS users (
            id uuid NOT NULL DEFAULT (gen_random_uuid()) PRIMARY KEY,
            username character varying(256) NOT NULL,
            password_hash bit(512) NOT NULL,
            email character varying(256) NULL,
            root uuid NOT NULL REFERENCES fsos (id) ON DELETE RESTRICT
        );

        CREATE TABLE IF NOT EXISTS ssh_keys (
            key character varying(256) NOT NULL,
            user_id uuid NOT NULL REFERENCES users (id) ON DELETE CASCADE,

            PRIMARY KEY (key, user_id)
        );

        CREATE TABLE IF NOT EXISTS fso_access (
            user_id uuid NOT NULL REFERENCES users (id) ON DELETE CASCADE,
            fso_id uuid NOT NULL REFERENCES fsos (id) ON DELETE CASCADE,

            PRIMARY KEY (user_id, fso_id)
        );
    END IF;
END $MIGRATION$;

DO
$MIGRATION$
BEGIN
    IF NOT EXISTS (SELECT * FROM migrations WHERE migration_name = 'change_type_group_owner_perms') THEN

        INSERT INTO migrations VALUES ('change_type_group_owner_perms');

        UPDATE fsos SET
        permissions = CASE
            WHEN fso_type = 'regular_file' THEN b'000110100100'
            WHEN fso_type = 'directory'    THEN b'000111101101'
            WHEN fso_type = 'symlink'      THEN b'000111111111'
        END
        WHERE permissions IS NULL;

        ALTER TABLE fsos
        DROP CONSTRAINT hierarchy_directory,
        DROP CONSTRAINT hierarchy_file,
        ADD CONSTRAINT hierarchy_file
            CHECK(fso_type <> 'regular_file' OR file_physical_path IS NOT NULL),
        ALTER permissions SET NOT NULL,
        ALTER fso_owner TYPE int USING (1000),
        ALTER fso_group TYPE int USING (100);
    END IF;
END $MIGRATION$;

DO
$MIGRATION$
BEGIN
    IF NOT EXISTS (SELECT * FROM migrations WHERE migration_name = 'add_group_owner_defaults') THEN

        INSERT INTO migrations VALUES ('add_group_owner_defaults');

        ALTER TABLE fsos
        ALTER fso_owner SET DEFAULT 1000,
        ALTER fso_group SET DEFAULT 100;
    END IF;
END $MIGRATION$;

DO
$MIGRATION$
BEGIN
    IF NOT EXISTS (SELECT * FROM migrations WHERE migration_name = 'remove_file_location') THEN

        INSERT INTO migrations VALUES ('remove_file_location');

        ALTER TABLE fsos DROP COLUMN file_physical_path;
    END IF;
END $MIGRATION$;

COMMIT;
