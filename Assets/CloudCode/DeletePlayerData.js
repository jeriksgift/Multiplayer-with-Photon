const { DataApi, FilesApi } = require('@unity-services/cloud-save-1.4');
const { RelationshipsApi } = require('@unity-services/friends-1.0');

module.exports = async ({ params, context }) => {
    if (!context.playerId || params.confirmation !== 'CONFIRM')
        throw new Error('A signed-in player must type CONFIRM to delete their data.');
    const { projectId, playerId } = context;
    const data = new DataApi(context);
    const files = new FilesApi(context);
    const relationships = new RelationshipsApi({ accessToken: context.accessToken });
    const ignoreMissing = async action => {
        try { return await action(); }
        catch (error) { if (error.response?.status !== 404) throw error; }
    };
    // Always fetch the first page: deleting shifts the remaining entries forward.
    for (let page = 0; page < 100; page++) {
        const response = await relationships.getRelationships(50, 0, false, false);
        if (!response.data.length) break;
        for (const relationship of response.data)
            await ignoreMissing(() => relationships.deleteRelationship(relationship.id));
        if (page === 99) throw new Error('Cleanup is not finished. Confirm deletion again to continue.');
    }
    for (let page = 0; page < 100; page++) {
        const response = await files.listPlayerFiles(projectId, playerId);
        if (!response.data.results.length) break;
        for (const file of response.data.results)
            await ignoreMissing(() => files.deleteFile(projectId, playerId, file.key));
        if (page === 99) throw new Error('Cleanup is not finished. Confirm deletion again to continue.');
    }
    await ignoreMissing(() => data.deleteItems(projectId, playerId));
    await ignoreMissing(() => data.deletePublicItems(projectId, playerId));
    const ownerEntity = 'toys-player-' + playerId;
    const owner = await ignoreMissing(() => data.getPrivateCustomItems(projectId, ownerEntity, ['identity']));
    const identity = owner?.data.results.find(item => item.key === 'identity');
    for (let attempt = 0; attempt < 12; attempt++) {
        const registry = await ignoreMissing(() => data.getPrivateCustomItems(projectId, 'toys-player-id-registry', ['allocator']));
        const allocator = registry?.data.results.find(item => item.key === 'allocator');
        if (!allocator?.value.pending?.[playerId]) break;
        const pending = { ...allocator.value.pending };
        delete pending[playerId];
        try {
            await data.setPrivateCustomItem(projectId, 'toys-player-id-registry', {
                key: 'allocator', value: { next: allocator.value.next, pending }, writeLock: allocator.writeLock
            });
            break;
        } catch (error) {
            if (error.response?.status !== 409 || attempt === 11) throw error;
        }
    }
    if (identity) {
        await ignoreMissing(() => data.deletePrivateCustomItems(projectId, 'toys-id-' + identity.value.publicId));
        await ignoreMissing(() => data.deletePrivateCustomItems(projectId, ownerEntity));
    }
    await ignoreMissing(() => data.deleteProtectedItems(projectId, playerId));
    // DeleteAccountAsync is called by the client only after every cleanup step succeeds.
    return { cleaned: true };
};

module.exports.params = { confirmation: { type: 'String', required: true } };
