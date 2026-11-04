const { DataApi } = require('@unity-services/cloud-save-1.4');

const REGISTRY = 'toys-player-id-registry';
const OWNER = playerId => 'toys-player-' + playerId;
const NUMBER = publicId => 'toys-id-' + publicId;
const validId = value => typeof value === 'string' && /^[1-9][0-9]{9}$/.test(value);
const conflict = error => error.response?.status === 409;

module.exports = async ({ params, context }) => {
    if (!context.playerId) throw new Error('A signed-in player is required.');
    const api = new DataApi(context);
    const project = context.projectId;
    const read = async (entity, key) => {
        try {
            const response = await api.getPrivateCustomItems(project, entity, [key]);
            return response.data.results.find(item => item.key === key) || null;
        } catch (error) { if (error.response?.status === 404) return null; throw error; }
    };
    const write = (entity, key, value, writeLock) => api.setPrivateCustomItem(project, entity,
        writeLock ? { key, value, writeLock } : { key, value });

    if (params.action === 'resolve') {
        if (!validId(params.publicId)) throw new Error('Enter a 10-digit player ID.');
        const entry = await read(NUMBER(params.publicId), 'identity');
        if (!entry) return { publicId: params.publicId, playerId: null };
        const owner = await read(OWNER(entry.value.playerId), 'identity');
        if (!owner || owner.value.publicId !== params.publicId) return { publicId: params.publicId, playerId: null };
        return { publicId: params.publicId, playerId: entry.value.playerId };
    }
    if (params.action === 'lookup') {
        const ids = [...new Set((params.playerIds || '').split(',').filter(Boolean))];
        if (ids.length > 100 || ids.some(id => !/^[A-Za-z0-9_-]{1,128}$/.test(id)))
            throw new Error('Invalid player ID lookup.');
        const result = {};
        for (const id of ids) {
            const entry = await read(OWNER(id), 'identity');
            if (entry && validId(entry.value.publicId)) result[id] = entry.value.publicId;
        }
        return { ids: result };
    }
    if (params.action !== 'get') throw new Error('Unknown player ID action.');

    const ownerEntity = OWNER(context.playerId);
    let owner = await read(ownerEntity, 'identity');
    if (owner) return owner.value;
    const profile = await api.getProtectedItems(project, context.playerId, ['toys_profile']);
    if (!profile.data.results.some(item => item.key === 'toys_profile' && item.value.age >= 16))
        throw new Error('Complete your player profile first.');

    let publicId;
    for (let attempt = 0; attempt < 12; attempt++) {
        const allocator = await read(REGISTRY, 'allocator');
        if (!allocator || !allocator.writeLock)
            throw new Error('Player ID registry is not initialized. Create the private allocator item using the setup guide.');
        // Read the owner after the allocator so a concurrent finalization invalidates
        // our allocator write lock before another number can be assigned to this owner.
        owner = await read(ownerEntity, 'identity');
        if (owner) return owner.value;
        const state = allocator.value;
        if (!Number.isSafeInteger(state.next) || state.next < 1000000000 || !state.pending || typeof state.pending !== 'object')
            throw new Error('Player ID registry is invalid.');
        if (state.pending[context.playerId]) {
            publicId = state.pending[context.playerId];
            if (!validId(publicId)) throw new Error('Player ID reservation is invalid.');
            break;
        }
        if (state.next > 9999999999) throw new Error('The 10-digit player ID range is exhausted.');
        publicId = String(state.next);
        const updated = { next: state.next + 1, pending: { ...state.pending, [context.playerId]: publicId } };
        try { await write(REGISTRY, 'allocator', updated, allocator.writeLock); break; }
        catch (error) { publicId = null; if (!conflict(error)) throw error; }
    }
    if (!publicId) throw new Error('Player IDs are busy. Please try again.');
    const identity = { publicId, playerId: context.playerId };
    await write(NUMBER(publicId), 'identity', identity);
    await write(ownerEntity, 'identity', identity);
    // The reservation survives failed requests; retries finish the same assignment.
    for (let attempt = 0; attempt < 12; attempt++) {
        const allocator = await read(REGISTRY, 'allocator');
        if (!allocator || !allocator.value.pending[context.playerId]) break;
        const pending = { ...allocator.value.pending };
        delete pending[context.playerId];
        try { await write(REGISTRY, 'allocator', { next: allocator.value.next, pending }, allocator.writeLock); break; }
        catch (error) { if (!conflict(error)) throw error; }
    }
    return identity;
};

module.exports.params = {
    action: { type: 'String', required: true },
    publicId: { type: 'String', required: false },
    playerIds: { type: 'String', required: false }
};
