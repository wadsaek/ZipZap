START TRANSACTION;

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


COMMIT;
