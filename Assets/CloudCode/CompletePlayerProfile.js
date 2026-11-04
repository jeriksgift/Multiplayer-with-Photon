const { DataApi } = require('@unity-services/cloud-save-1.4');

module.exports = async ({ params, context }) => {
    if (!context.playerId) throw new Error('A signed-in player is required.');
    const nickname = typeof params.nickname === 'string' ? params.nickname.trim() : '';
    if (nickname.length < 2 || nickname.length > 16 || /[\s\u0000-\u001f\u007f<>]/u.test(nickname))
        throw new Error('Nickname must have 2–16 characters with no whitespace, markup, or control characters.');
    if (!Number.isInteger(params.age) || params.age < 16 || params.age > 120)
        throw new Error('You must be at least 16. Enter an age from 16 to 120.');
    const api = new DataApi(context);
    const response = await api.getProtectedItems(context.projectId, context.playerId, ['toys_profile']);
    const existing = response.data.results.find(item => item.key === 'toys_profile');
    if (existing) return existing.value;
    const profile = { nickname, age: params.age, version: 1 };
    await api.setProtectedItem(context.projectId, context.playerId, { key: 'toys_profile', value: profile });
    return profile;
};

module.exports.params = {
    nickname: { type: 'String', required: true },
    age: { type: 'Numeric', required: true }
};
