const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

function script(name, services) {
    const context = { module: { exports: {} }, require: id => {
        if (!services[id]) throw new Error('Unexpected SDK: ' + id);
        return services[id];
    }};
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../../Assets/CloudCode', name + '.js'), 'utf8'), context);
    return context.module.exports;
}
const identity = { projectId: 'project', playerId: 'current-player', accessToken: 'player-token' };

test('profile: rejects under-16, noninteger, malformed nickname and missing identity', async () => {
    const run = script('CompletePlayerProfile', { '@unity-services/cloud-save-1.4': { DataApi: class { constructor() { throw new Error('Validation must happen before any service call'); } } } });
    for (const params of [{ nickname:'Valid', age:15 }, { nickname:'Valid', age:16.5 }, { nickname:'Valid', age:121 }, { nickname:'<bad>', age:16 }, { nickname:'a', age:16 }, { nickname:'bad name', age:16 }])
        await assert.rejects(run({ params, context:identity }), /must|Nickname|least/);
    await assert.rejects(run({ params:{ nickname:'Valid', age:16 }, context:{} }), /signed-in/);
});

test('profile: age 16 is accepted and writes only to the authenticated player protected data', async () => {
    let saved;
    const run = script('CompletePlayerProfile', { '@unity-services/cloud-save-1.4': { DataApi: class {
        async getProtectedItems() { return { data: { results: [] } }; }
        async setProtectedItem(project, player, item) { saved = { project, player, item }; }
    }}});
    const profile = await run({ params:{ nickname:'  ToyPlayer  ', age:16, playerId:'victim' }, context:identity });
    assert.equal(profile.nickname, 'ToyPlayer'); assert.equal(profile.age, 16);
    assert.equal(saved.player, 'current-player'); assert.equal(saved.item.key, 'toys_profile');
});

test('profile: retry preserves the already completed profile', async () => {
    const original = { nickname:'Original', age:18, version:1 };
    const run = script('CompletePlayerProfile', { '@unity-services/cloud-save-1.4': { DataApi: class {
        async getProtectedItems() { return { data: { results:[{ key:'toys_profile', value:original }] } }; }
        async setProtectedItem() { assert.fail('Retry must not overwrite a completed profile'); }
    }}});
    assert.equal(await run({ params:{ nickname:'NewName', age:20 }, context:identity }), original);
});

function deletionHarness({ failAt, missing = false } = {}) {
    const calls = [];
    const friends = Array.from({length:121}, (_,i) => ({ id:'r' + i }));
    const files = Array.from({length:43}, (_,i) => ({ key:'f' + i }));
    const clear = async (name, project, player) => {
        assert.equal(player, identity.playerId); calls.push(name);
        if (name === failAt) throw { response: { status:503 } };
        if (missing) throw { response: { status:404 } };
    };
    const run = script('DeletePlayerData', {
        '@unity-services/cloud-save-1.4': {
            DataApi:class {
                async getPrivateCustomItems() { return { data: { results: [] } }; }
                deleteItems(p,u) { return clear('default',p,u); }
                deletePublicItems(p,u) { return clear('public',p,u); }
                deleteProtectedItems(p,u) { return clear('protected',p,u); }
            },
            FilesApi:class {
                async listPlayerFiles() { return { data:{ results:files.slice(0,20) } }; }
                async deleteFile(p,u,key) { files.splice(files.findIndex(f => f.key === key),1); }
            }
        },
        '@unity-services/friends-1.0': { RelationshipsApi:class {
            constructor(config) { assert.equal(config.accessToken, identity.accessToken); }
            async getRelationships(limit, offset) { assert.equal(offset,0); return { data:friends.slice(0,limit) }; }
            async deleteRelationship(id) { friends.splice(friends.findIndex(f => f.id === id),1); }
        }}
    });
    return { run, calls, friends, files };
}

test('deletion: requires exact CONFIRM before any deletion', async () => {
    const h = deletionHarness();
    for (const confirmation of ['confirm', 'CONFIRM ', '', undefined])
        await assert.rejects(h.run({ params:{ confirmation }, context:identity }), /CONFIRM/);
    assert.equal(h.friends.length,121); assert.equal(h.files.length,43); assert.equal(h.calls.length,0);
});

test('deletion: exhausts paginated friendships/files and all three save classes', async () => {
    const h = deletionHarness();
    const result = await h.run({ params:{ confirmation:'CONFIRM', playerId:'victim' }, context:identity });
    assert.equal(result.cleaned,true); assert.equal(h.friends.length,0); assert.equal(h.files.length,0);
    assert.deepEqual(h.calls,['default','public','protected']);
});

test('deletion: service failure interrupts completion and permits retry', async () => {
    const h = deletionHarness({ failAt:'public' });
    await assert.rejects(h.run({ params:{ confirmation:'CONFIRM' }, context:identity }));
    assert.deepEqual(h.calls,['default','public']);
});

test('deletion: already absent data is idempotent', async () => {
    const h = deletionHarness({ missing:true });
    assert.equal((await h.run({ params:{ confirmation:'CONFIRM' }, context:identity })).cleaned,true);
});
