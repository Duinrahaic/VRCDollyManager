window.triggerDownload = (fileName, dataUri) => {
    const link = document.createElement("a");
    link.href = dataUri;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
