-- SQL Query to create the MOD role in ASP.NET Core Identity
-- This assumes you're using the default Identity schema

-- Insert the MOD role into the AspNetRoles table
-- The Id is a GUID that should be generated, and Name/NormalizedName should be 'Mod'/'MOD'
INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
VALUES (
    NEWID(),                    -- Or use a specific GUID like '00000000-0000-0000-0000-000000000003'
    'Mod',                      -- Role name (case-sensitive in display)
    'MOD',                      -- Normalized name (uppercase for case-insensitive lookups)
    NEWID()                     -- Concurrency stamp (random GUID)
);

-- Alternative with specific GUID (recommended for consistency across environments):
-- INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
-- VALUES (
--     '4d8c1f3e-7b2a-4f9c-a1e5-6d3b8c9f2a1d',  -- Fixed GUID for Mod role
--     'Mod',
--     'MOD',
--     NEWID()
-- );

-- To assign a user to the MOD role, use:
-- INSERT INTO AspNetUserRoles (UserId, RoleId)
-- VALUES (
--     '<USER_ID>',              -- The Id of the user from AspNetUsers table
--     '<MOD_ROLE_ID>'           -- The Id from AspNetRoles table for Mod role
-- );

-- Example to get the Mod role ID:
-- SELECT Id FROM AspNetRoles WHERE NormalizedName = 'MOD';
