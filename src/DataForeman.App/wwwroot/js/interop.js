// DataForeman interop helpers
window.dataforeman = {
    downloadJson: function (fileName, base64Content) {
        var a = document.createElement('a');
        a.href = 'data:application/json;base64,' + base64Content;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    }
};
