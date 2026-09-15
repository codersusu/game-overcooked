mergeInto(LibraryManager.library, {
  BaraVoiceKeyShow: function(name,value) {window.BaraLive?.keyInput.show(UTF8ToString(name),UTF8ToString(value));},
  BaraVoiceKeyLayout: function(x,y,w,h) {window.BaraLive?.keyInput.layout(x,y,w,h);},
  BaraVoiceKeyValue: function() {const value=window.BaraLive?.keyInput.value()||'',n=lengthBytesUTF8(value)+1,p=_malloc(n);stringToUTF8(value,p,n);return p;},
  BaraVoiceKeyClear: function() {window.BaraLive?.keyInput.clear();},
  BaraVoiceKeyHide: function() {window.BaraLive?.keyInput.hide();},
  BaraLiveStart: function(name, context, volume, key) {
    const objectName=UTF8ToString(name), snapshot=JSON.parse(UTF8ToString(context));
    const emit=data=>SendMessage(objectName,'OnLiveEvent',JSON.stringify(data));
    if(!window.BaraLive){emit({type:'error',message:'Open the game from its main index page to use live chat.'});return;}
    window.BaraLive.start(snapshot,emit,volume,UTF8ToString(key));
  },
  BaraLiveContext: function(context,volume) {if(window.BaraLive){window.BaraLive.volume(volume);window.BaraLive.update(JSON.parse(UTF8ToString(context)));}},
  BaraLiveStop: function() {if(window.BaraLive)window.BaraLive.stop();}
});
