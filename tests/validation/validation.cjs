const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { JSDOM } = require('jsdom');
const assets = path.resolve(__dirname, '../../WeatherZilla.WebApp/wwwroot/lib');
const validator = process.env.VALIDATOR_ROOT || path.join(assets, 'jquery-validation/dist');
for (const suffix of ['', '.min']) {
  const dom = new JSDOM('<form><input name="email" data-val="true" data-val-required="Required" data-val-email="Email" type="text"></form>', { runScripts: 'outside-only', url: 'https://example.test' });
  const context = dom.getInternalVMContext();
  for (const file of [path.join(assets, 'jquery/dist/jquery.js'), path.join(validator, `jquery.validate${suffix}.js`), path.join(validator, `additional-methods${suffix}.js`), path.join(assets, 'jquery-validation-unobtrusive/jquery.validate.unobtrusive.js')]) {
    vm.runInContext(fs.readFileSync(file, 'utf8'), context);
  }
  function validate(method, value, optional = false) {
    context.inputValue = value; context.optionalValue = optional;
    return vm.runInContext(`$.validator.methods.${method}.call({optional:()=>optionalValue}, inputValue, {})`, context, { timeout: 500 });
  }
  for (const method of ['url', 'url2']) {
    for (const url of ['https://example.com', 'http://example.com/path?q=hello', 'ftp://example.com', 'https://user:pass@example.com', 'https://user%20name:pass%21@example.com']) assert.equal(validate(method, url), true, `${method}: ${url}`);
    for (const url of ['not a URL', 'javascript:alert(1)', 'http://' + 'a.'.repeat(1000) + '!', 'http://' + 'a'.repeat(30000) + '.aa!!', 'http://' + 'a:'.repeat(10000) + '!', 'http://' + '%aa'.repeat(1000) + '!', 'http://user:' + '%aa'.repeat(1000) + '!']) assert.equal(validate(method, url), false);
    assert.equal(validate(method, '', true), true);
  }
  assert.equal(validate('url2', 'http://localhost'), true);
  assert.equal(validate('url', 'http://localhost'), false);
  vm.runInContext('$.validator.unobtrusive.parse(document);', context);
  const rules = context.$('input').rules();
  assert.equal(rules.required, true); assert.equal(rules.email, true);
  assert.equal(validate('email', 'person@example.com'), true);
  assert.equal(validate('email', 'not-an-email'), false);
  dom.window.close();
  console.log(`PASS ${suffix || 'unminified'}: bounded ReDoS cases, legitimate URLs, optional fields and ASP.NET unobtrusive rules`);
}
