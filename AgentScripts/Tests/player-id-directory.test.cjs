const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const clone = value => JSON.parse(JSON.stringify(value));
const tick = () => new Promise(resolve => setImmediate(resolve));

function directory({ seeded = true, failOwnerOnce = false, next = 1000000000 } = {}) {
    const store = new Map();
    if (seeded) store.set('toys-player-id-registry:allocator', { key:'allocator', value:{next,pending:{}}, writeLock:'1' });
    let version = 1;
    class DataApi {
        async getPrivateCustomItems(project, entity, keys) {
            await tick();
            return { data:{ results:keys.map(key => store.get(entity + ':' + key)).filter(Boolean).map(clone) } };
        }
        async setPrivateCustomItem(project, entity, item) {
            await tick();
            if (failOwnerOnce && entity.startsWith('toys-player-') && entity !== 'toys-player-id-registry') {
                failOwnerOnce = false; throw new Error('Interrupted owner write');
            }
            const key = entity + ':' + item.key;
            const current = store.get(key);
            if (item.writeLock && current?.writeLock !== item.writeLock) throw { response:{status:409} };
            store.set(key, { key:item.key, value:clone(item.value), writeLock:String(++version) });
        }
        async getProtectedItems() { return { data:{results:[{key:'toys_profile',value:{age:19,nickname:'Player'}}]} }; }
    }
    const sandbox = { module:{exports:{}}, require:id => {
        assert.equal(id,'@unity-services/cloud-save-1.4'); return {DataApi};
    }};
    vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../../Assets/CloudCode/PlayerIdDirectory.js'),'utf8'),sandbox);
    const call = (playerId,params = {action:'get'}) => sandbox.module.exports({params,context:{playerId,projectId:'project'}});
    return {call,store};
}

test('IDs: persistent, 10 digits, and resolvable back to the correct owner', async () => {
    const h = directory();
    const first = await h.call('alice');
    assert.match(first.publicId,/^[1-9][0-9]{9}$/);
    assert.equal(first.publicId,'1000000000');
    assert.equal((await h.call('alice')).publicId,first.publicId);
    assert.equal((await h.call('bob',{action:'resolve',publicId:first.publicId})).playerId,'alice');
    assert.equal((await h.call('bob',{action:'lookup',playerIds:'alice,alice'})).ids.alice,first.publicId);
});

test('IDs: simultaneous registrations never give two players the same number', async () => {
    const h = directory();
    const results = await Promise.all(Array.from({length:8},(_,i) => h.call('player'+i)));
    assert.equal(new Set(results.map(r=>r.publicId)).size,8);
    for (const result of results) assert.match(result.publicId,/^[1-9][0-9]{9}$/);
});

test('IDs: simultaneous devices for one account keep the same number', async () => {
    const h = directory();
    const results = await Promise.all(Array.from({length:8},()=>h.call('same-player')));
    assert.equal(new Set(results.map(r=>r.publicId)).size,1);
    assert.equal(h.store.get('toys-player-id-registry:allocator').value.next,1000000001);
});

test('IDs: a failed write resumes the reservation without consuming a different number', async () => {
    const h = directory({failOwnerOnce:true});
    await assert.rejects(h.call('alice'),/Interrupted/);
    assert.equal((await h.call('alice')).publicId,'1000000000');
    const allocator = h.store.get('toys-player-id-registry:allocator').value;
    assert.equal(allocator.next,1000000001); assert.deepEqual(allocator.pending,{});
});

test('IDs: missing registry fails closed rather than assigning duplicate local IDs', async () => {
    const h = directory({seeded:false});
    await assert.rejects(h.call('alice'),/not initialized/);
    assert.equal(h.store.size,0);
});

test('IDs: input validation, unknown numbers, and exhausted 10-digit range', async () => {
    const h = directory();
    for (const publicId of ['123','0123456789','12345678901','abcdefghij'])
        await assert.rejects(h.call('alice',{action:'resolve',publicId}),/10-digit/);
    assert.equal((await h.call('alice',{action:'resolve',publicId:'9999999999'})).playerId,null);
    await assert.rejects(h.call('alice',{action:'lookup',playerIds:'bad/id'}),/Invalid/);
    await assert.rejects(directory({next:10000000000}).call('alice'),/exhausted/);
});

test('IDs: the upper-bound number is exactly 10 digits and is issued only once', async () => {
    const h = directory({next:9999999999});
    assert.equal((await h.call('alice')).publicId,'9999999999');
    await assert.rejects(h.call('bob'),/exhausted/);
});

test('deletion: removes numeric directory entries and the owner reservation without resetting the counter', async () => {
    const playerId = 'current-player';
    const state = {next:1000000002,pending:{[playerId]:'1000000000',other:'1000000001'}};
    const entries = new Map([
        ['toys-player-id-registry:allocator',{key:'allocator',value:state,writeLock:'v1'}],
        ['toys-player-'+playerId+':identity',{key:'identity',value:{playerId,publicId:'1000000000'}}],
        ['toys-id-1000000000:identity',{key:'identity',value:{playerId,publicId:'1000000000'}}]
    ]);
    const sandbox = {module:{exports:{}},require:id => {
        if (id === '@unity-services/friends-1.0') return {RelationshipsApi:class {async getRelationships(){return {data:[]};}}};
        return {
            FilesApi:class {async listPlayerFiles(){return {data:{results:[]}};}},
            DataApi:class {
                async deleteItems(){} async deletePublicItems(){} async deleteProtectedItems(){}
                async getPrivateCustomItems(project,entity,keys){return {data:{results:keys.map(key=>entries.get(entity+':'+key)).filter(Boolean).map(clone)}};}
                async setPrivateCustomItem(project,entity,item){entries.set(entity+':'+item.key,clone(item));}
                async deletePrivateCustomItems(project,entity){for(const key of entries.keys())if(key.startsWith(entity+':'))entries.delete(key);}
            }
        };
    }};
    vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../../Assets/CloudCode/DeletePlayerData.js'),'utf8'),sandbox);
    await sandbox.module.exports({params:{confirmation:'CONFIRM'},context:{playerId,projectId:'project',accessToken:'token'}});
    assert.equal(entries.has('toys-player-'+playerId+':identity'),false);
    assert.equal(entries.has('toys-id-1000000000:identity'),false);
    const allocator = entries.get('toys-player-id-registry:allocator').value;
    assert.equal(allocator.next,1000000002);assert.deepEqual(allocator.pending,{other:'1000000001'});
});
